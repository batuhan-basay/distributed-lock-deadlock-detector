using LockGuard.Core.Abstractions;
using LockGuard.Core.Coordination;
using LockGuard.Core.Diagnostics;
using LockGuard.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LockGuard.Core.Reclaim;

public enum StaleLockReason
{
    /// <summary>The owning instance has never sent a heartbeat (or its heartbeat entry was purged).</summary>
    OwnerInstanceUnknown,

    /// <summary>The owning instance stopped sending heartbeats: it crashed / was killed / is partitioned.</summary>
    OwnerInstanceDead,

    /// <summary>The key has no expiry; it would live forever (legacy client or manual SET).</summary>
    NoExpiry,

    /// <summary>The lock is held longer than the allowed maximum: zombie process whose watchdog keeps it alive.</summary>
    MaxHoldTimeExceeded,
}

/// <summary>
/// Finds stale / orphaned locks and releases them safely.
/// <para>
/// Safety: release is a compare-and-delete on the <em>observed</em> owner, so if the lock was released and re-acquired by
/// a healthy session between the scan and the release, nothing happens. The previous owner also gets an abort signal so
/// that a zombie that is still alive stops working on the resource (its watchdog will report the lease as lost too).
/// </para>
/// </summary>
public sealed partial class OrphanedLockReclaimer
{
    private readonly ILockStore _store;
    private readonly ICoordinationStore _coordination;
    private readonly LockGuardTelemetry _telemetry;
    private readonly TimeProvider _time;
    private readonly LockGuardOptions _options;
    private readonly ILogger<OrphanedLockReclaimer> _logger;

    public OrphanedLockReclaimer(
        ILockStore store,
        ICoordinationStore coordination,
        LockGuardTelemetry telemetry,
        TimeProvider time,
        IOptions<LockGuardOptions> options,
        ILogger<OrphanedLockReclaimer> logger)
    {
        _store = store;
        _coordination = coordination;
        _telemetry = telemetry;
        _time = time;
        _options = options.Value;
        _logger = logger;
    }

    private ReclaimerOptions Options => _options.Reclaimer;

    /// <summary>Pure classification logic (unit-tested). Returns <c>null</c> when the lock is healthy.</summary>
    public StaleLockReason? Classify(LockInfo lockInfo, IReadOnlyDictionary<string, InstanceHeartbeat> instances, DateTimeOffset now)
    {
        if (LockGuardInternal.IsInternal(lockInfo.Resource))
        {
            return null;
        }

        if (lockInfo.HeldFor(now) < Options.MinLockAge)
        {
            return null; // too young: the owner may not have published its first heartbeat yet
        }

        if (!instances.TryGetValue(lockInfo.InstanceId, out var heartbeat))
        {
            return StaleLockReason.OwnerInstanceUnknown;
        }

        if (now - heartbeat.LastSeenAt > Options.InstanceStaleAfter)
        {
            return StaleLockReason.OwnerInstanceDead;
        }

        if (lockInfo.HasNoExpiry && Options.ReclaimLocksWithoutExpiry)
        {
            return StaleLockReason.NoExpiry;
        }

        if (lockInfo.HeldFor(now) > Options.MaxHoldTime)
        {
            return StaleLockReason.MaxHoldTimeExceeded;
        }

        return null;
    }

    /// <summary>One reclamation pass. Returns the reports of the locks that were released.</summary>
    public async Task<IReadOnlyList<LockEventReport>> ScanAsync(CancellationToken cancellationToken = default)
    {
        using var activity = LockGuardTelemetry.ActivitySource.StartActivity("lockguard.reclaimer.scan");
        var now = _time.GetUtcNow();
        var locks = await _store.ListAsync(cancellationToken);
        var instances = await _coordination.GetInstancesAsync(cancellationToken);
        _telemetry.SetTrackedLocks(locks.Count(l => !LockGuardInternal.IsInternal(l.Resource)));

        var reports = new List<LockEventReport>();
        foreach (var lockInfo in locks)
        {
            var reason = Classify(lockInfo, instances, now);
            if (reason is null)
            {
                continue;
            }

            if (!await _store.ReleaseAsync(lockInfo.Resource, lockInfo.SessionId, cancellationToken))
            {
                continue; // owner changed meanwhile: not ours to touch
            }

            var reasonText = ToMetricTag(reason.Value);
            _telemetry.LocksReclaimed.Add(1,
                new KeyValuePair<string, object?>("reason", reasonText),
                new KeyValuePair<string, object?>("resource_group", LockGuardTelemetry.ResourceGroup(lockInfo.Resource)));
            LogReclaimed(lockInfo.Resource, lockInfo.SessionId, lockInfo.InstanceId, reason.Value);

            await _coordination.SignalAbortAsync(
                new AbortSignal(lockInfo.SessionId, AbortReason.LockReclaimed, $"lock '{lockInfo.Resource}' reclaimed: {reasonText}", now),
                _options.Detection.AbortSignalTtl,
                cancellationToken);

            var report = new LockEventReport(
                Id: Guid.CreateVersion7().ToString("N"),
                Kind: LockEventKind.OrphanedLockReclaimed,
                OccurredAt: now,
                Summary: $"Reclaimed '{lockInfo.Resource}' from session {lockInfo.SessionId} on instance {lockInfo.InstanceId} " +
                         $"(held {lockInfo.HeldFor(now).TotalSeconds:F1}s, reason: {reasonText}).",
                VictimSessionId: lockInfo.SessionId,
                Resource: lockInfo.Resource,
                PreviousOwnerSessionId: lockInfo.SessionId,
                Reason: reasonText,
                DetectedBy: _options.InstanceId);

            await _coordination.AppendEventAsync(report, cancellationToken);
            reports.Add(report);
        }

        await PurgeDeadInstancesAsync(instances, now, cancellationToken);
        return reports;
    }

    /// <summary>Removes sessions, wait records and heartbeat entries left behind by crashed instances.</summary>
    private async Task PurgeDeadInstancesAsync(IReadOnlyDictionary<string, InstanceHeartbeat> instances, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Keep dead instances visible for a while (3x the stale threshold) so their locks are classified as "dead" first.
        var purgeAfter = Options.InstanceStaleAfter * 3;
        var dead = instances.Values
            .Where(i => now - i.LastSeenAt > Options.InstanceStaleAfter)
            .Select(i => i.InstanceId)
            .ToHashSet(StringComparer.Ordinal);

        if (dead.Count == 0)
        {
            return;
        }

        foreach (var wait in await _coordination.GetWaitsAsync(cancellationToken))
        {
            if (dead.Contains(wait.InstanceId))
            {
                await _coordination.ClearWaitAsync(wait.WaiterSessionId, cancellationToken);
            }
        }

        foreach (var session in (await _coordination.GetSessionsAsync(cancellationToken)).Values)
        {
            if (dead.Contains(session.InstanceId))
            {
                await _coordination.RemoveSessionAsync(session.SessionId, cancellationToken);
            }
        }

        foreach (var instance in instances.Values.Where(i => now - i.LastSeenAt > purgeAfter))
        {
            await _coordination.RemoveInstanceAsync(instance.InstanceId, cancellationToken);
        }
    }

    public static string ToMetricTag(StaleLockReason reason) => reason switch
    {
        StaleLockReason.OwnerInstanceUnknown => "owner_instance_unknown",
        StaleLockReason.OwnerInstanceDead => "owner_instance_dead",
        StaleLockReason.NoExpiry => "no_expiry",
        StaleLockReason.MaxHoldTimeExceeded => "max_hold_time_exceeded",
        _ => "unknown",
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reclaimed stale lock {Resource} (session {SessionId}, instance {InstanceId}): {Reason}")]
    private partial void LogReclaimed(string resource, string sessionId, string instanceId, StaleLockReason reason);
}

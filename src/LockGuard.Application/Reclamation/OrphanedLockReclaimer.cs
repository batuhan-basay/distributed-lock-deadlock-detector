using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Application.Common.Options;
using LockGuard.Application.Common.Telemetry;
using LockGuard.Domain.Coordination;
using LockGuard.Domain.Events;
using LockGuard.Domain.Locks;
using LockGuard.Domain.Reclamation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LockGuard.Application.Reclamation;

public sealed partial class OrphanedLockReclaimer
{
    private readonly ILockStore _store;
    private readonly ICoordinationStore _coordination;
    private readonly LockGuardTelemetry _telemetry;
    private readonly TimeProvider _time;
    private readonly LockGuardOptions _options;
    private readonly StaleLockPolicy _policy;
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
        _policy = _options.Reclaimer.ToPolicy();
        _logger = logger;
    }

    public async Task<IReadOnlyList<LockEventReport>> ScanAsync(CancellationToken cancellationToken = default)
    {
        using var activity = LockGuardTelemetry.ActivitySource.StartActivity("lockguard.reclaimer.scan");
        var now = _time.GetUtcNow();
        var locks = await _store.ListAsync(cancellationToken);
        var instances = await _coordination.GetInstancesAsync(cancellationToken);
        _telemetry.SetTrackedLocks(locks.Count(l => !InternalResources.IsInternal(l.Resource)));

        var reports = new List<LockEventReport>();
        foreach (var lockInfo in locks)
        {
            var reason = _policy.Classify(lockInfo, instances, now);
            if (reason is null)
            {
                continue;
            }

            if (!await _store.ReleaseAsync(lockInfo.Resource, lockInfo.SessionId, cancellationToken))
            {
                continue; 
            }

            var tag = StaleLockPolicy.ToTag(reason.Value);
            _telemetry.LocksReclaimed.Add(1,
                new KeyValuePair<string, object?>("reason", tag),
                new KeyValuePair<string, object?>("resource_group", ResourceGroup.Of(lockInfo.Resource)));
            LogReclaimed(lockInfo.Resource, lockInfo.SessionId, lockInfo.InstanceId, reason.Value);

            await _coordination.SignalAbortAsync(
                new AbortSignal(lockInfo.SessionId, AbortReason.LockReclaimed, $"lock '{lockInfo.Resource}' reclaimed: {tag}", now),
                _options.Detection.AbortSignalTtl,
                cancellationToken);

            var report = new LockEventReport(
                Id: Guid.CreateVersion7().ToString("N"),
                Kind: LockEventKind.OrphanedLockReclaimed,
                OccurredAt: now,
                Summary: $"Reclaimed '{lockInfo.Resource}' from session {lockInfo.SessionId} on instance {lockInfo.InstanceId} " +
                         $"(held {lockInfo.HeldFor(now).TotalSeconds:F1}s, reason: {tag}).",
                VictimSessionId: lockInfo.SessionId,
                Resource: lockInfo.Resource,
                PreviousOwnerSessionId: lockInfo.SessionId,
                Reason: tag,
                DetectedBy: _options.InstanceId);

            await _coordination.AppendEventAsync(report, cancellationToken);
            reports.Add(report);
        }

        await PurgeDeadInstancesAsync(instances, now, cancellationToken);
        return reports;
    }

    private async Task PurgeDeadInstancesAsync(IReadOnlyDictionary<string, InstanceHeartbeat> instances, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var staleAfter = _options.Reclaimer.InstanceStaleAfter;

        var purgeAfter = staleAfter * 3;
        var dead = instances.Values
            .Where(i => now - i.LastSeenAt > staleAfter)
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reclaimed stale lock {Resource} (session {SessionId}, instance {InstanceId}): {Reason}")]
    private partial void LogReclaimed(string resource, string sessionId, string instanceId, StaleLockReason reason);
}

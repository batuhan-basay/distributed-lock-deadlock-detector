using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Application.Common.Options;
using LockGuard.Application.Common.Telemetry;
using LockGuard.Domain.Coordination;
using LockGuard.Domain.Deadlocks;
using LockGuard.Domain.Events;
using LockGuard.Domain.Exceptions;
using LockGuard.Domain.Locks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LockGuard.Application.Detection;

public sealed partial class DeadlockDetector
{
    private readonly ILockStore _store;
    private readonly ICoordinationStore _coordination;
    private readonly IDeadlockVictimSelector _victimSelector;
    private readonly LockGuardTelemetry _telemetry;
    private readonly TimeProvider _time;
    private readonly LockGuardOptions _options;
    private readonly ILogger<DeadlockDetector> _logger;
    private readonly Dictionary<string, TrackedCycle> _tracked = new(StringComparer.Ordinal);

    public DeadlockDetector(
        ILockStore store,
        ICoordinationStore coordination,
        IDeadlockVictimSelector victimSelector,
        LockGuardTelemetry telemetry,
        TimeProvider time,
        IOptions<LockGuardOptions> options,
        ILogger<DeadlockDetector> logger)
    {
        _store = store;
        _coordination = coordination;
        _victimSelector = victimSelector;
        _telemetry = telemetry;
        _time = time;
        _options = options.Value;
        _logger = logger;
    }

    private DeadlockDetectionOptions Options => _options.Detection;

    public async Task<WaitForGraphSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var (graph, _) = await BuildGraphAsync(cancellationToken);
        return new WaitForGraphSnapshot(_time.GetUtcNow(), graph.Edges, graph.FindCycles());
    }

    public async Task<WaitForGraphSnapshot> ScanAsync(CancellationToken cancellationToken = default)
    {
        using var activity = LockGuardTelemetry.ActivitySource.StartActivity("lockguard.detector.scan");

        var (graph, locks) = await BuildGraphAsync(cancellationToken);
        var cycles = graph.FindCycles();
        var now = _time.GetUtcNow();
        _telemetry.SetWaitGraph(graph.Edges.Count, cycles.Count);
        activity?.SetTag("lockguard.wfg.edges", graph.Edges.Count);
        activity?.SetTag("lockguard.wfg.cycles", cycles.Count);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cycle in cycles)
        {
            seen.Add(cycle.Signature);
            if (!_tracked.TryGetValue(cycle.Signature, out var tracked))
            {
                _tracked[cycle.Signature] = tracked = new TrackedCycle(cycle, now);
            }

            tracked.Observations++;
            if (tracked.Observations < Math.Max(1, Options.ConfirmationScans))
            {
                continue;
            }

            if (tracked.VictimSessionId is null)
            {
                await SignalVictimAsync(tracked, locks, now, cancellationToken);
            }
            else if (!tracked.ForceReleased && now - tracked.SignalledAt >= Options.VictimForceReleaseAfter)
            {
                await ForceReleaseVictimAsync(tracked, locks, now, cancellationToken);
            }
        }

        foreach (var signature in _tracked.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            var gone = _tracked[signature];
            _tracked.Remove(signature);
            if (gone.VictimSessionId is not null)
            {
                await ReportResolvedAsync(gone, now, cancellationToken);
            }
        }

        return new WaitForGraphSnapshot(now, graph.Edges, cycles);
    }

    private async Task<(WaitForGraph Graph, IReadOnlyList<LockInfo> Locks)> BuildGraphAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var waits = (await _coordination.GetWaitsAsync(cancellationToken))
            .Where(w => now - w.LastSeenAt <= Options.WaitRecordStaleAfter)
            .ToList();

        var locks = await _store.ListAsync(cancellationToken);
        var holders = locks
            .Where(l => !InternalResources.IsInternal(l.Resource))
            .GroupBy(l => l.Resource, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().SessionId, StringComparer.Ordinal);

        return (WaitForGraph.Build(waits, holders), locks);
    }

    private async Task SignalVictimAsync(TrackedCycle tracked, IReadOnlyList<LockInfo> locks, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cycle = tracked.Cycle;
        var sessions = await _coordination.GetSessionsAsync(cancellationToken);
        var heldCounts = locks
            .GroupBy(l => l.SessionId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var victim = _victimSelector.SelectVictim(cycle, sessions, heldCounts);
        var description = cycle.Describe();
        var details = $"deadlock cycle [{description}] detected by {_options.InstanceId}; '{victim}' selected as victim";

        await _coordination.SignalAbortAsync(new AbortSignal(victim, AbortReason.DeadlockVictim, details, now), Options.AbortSignalTtl, cancellationToken);

        tracked.VictimSessionId = victim;
        tracked.SignalledAt = now;

        _telemetry.DeadlocksDetected.Add(1, new KeyValuePair<string, object?>("cycle_length", cycle.Sessions.Count));
        _telemetry.DeadlocksResolved.Add(1, new KeyValuePair<string, object?>("strategy", "victim_signal"));
        LogDeadlock(cycle.Sessions.Count, description, victim);

        await _coordination.AppendEventAsync(new LockEventReport(
            Id: NewId(),
            Kind: LockEventKind.DeadlockDetected,
            OccurredAt: now,
            Summary: $"Deadlock between {cycle.Sessions.Count} sessions: {description}. Victim: {victim}.",
            VictimSessionId: victim,
            Cycle: cycle.Edges,
            DetectedBy: _options.InstanceId), cancellationToken);
    }

    private async Task ForceReleaseVictimAsync(TrackedCycle tracked, IReadOnlyList<LockInfo> locks, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var victim = tracked.VictimSessionId!;
        var victimLocks = locks.Where(l => string.Equals(l.SessionId, victim, StringComparison.Ordinal)).ToList();
        var released = new List<string>();
        foreach (var lockInfo in victimLocks)
        {
            if (await _store.ReleaseAsync(lockInfo.Resource, victim, cancellationToken))
            {
                released.Add(lockInfo.Resource);
            }
        }

        await _coordination.ClearWaitAsync(victim, cancellationToken);
        tracked.ForceReleased = true;
        _telemetry.DeadlocksResolved.Add(1, new KeyValuePair<string, object?>("strategy", "force_release"));
        LogForceRelease(victim, released.Count);

        await _coordination.AppendEventAsync(new LockEventReport(
            Id: NewId(),
            Kind: LockEventKind.VictimForceReleased,
            OccurredAt: now,
            Summary: $"Victim {victim} did not react within {Options.VictimForceReleaseAfter.TotalSeconds:F0}s; force-released {released.Count} lock(s): {string.Join(", ", released)}.",
            VictimSessionId: victim,
            Cycle: tracked.Cycle.Edges,
            Reason: "victim_unresponsive",
            DetectedBy: _options.InstanceId), cancellationToken);
    }

    private async Task ReportResolvedAsync(TrackedCycle tracked, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var timeToResolve = now - tracked.FirstSeenAt;
        LogResolved(tracked.Cycle.Describe(), timeToResolve.TotalMilliseconds);
        await _coordination.AppendEventAsync(new LockEventReport(
            Id: NewId(),
            Kind: LockEventKind.DeadlockResolved,
            OccurredAt: now,
            Summary: $"Deadlock [{tracked.Cycle.Describe()}] resolved {timeToResolve.TotalMilliseconds:F0} ms after first detection by aborting {tracked.VictimSessionId}{(tracked.ForceReleased ? " (force release)" : string.Empty)}.",
            VictimSessionId: tracked.VictimSessionId,
            Cycle: tracked.Cycle.Edges,
            DetectedBy: _options.InstanceId), cancellationToken);
    }

    private static string NewId() => Guid.CreateVersion7().ToString("N");

    private sealed class TrackedCycle(DeadlockCycle cycle, DateTimeOffset firstSeenAt)
    {
        public DeadlockCycle Cycle { get; } = cycle;
        public DateTimeOffset FirstSeenAt { get; } = firstSeenAt;
        public int Observations { get; set; }
        public string? VictimSessionId { get; set; }
        public DateTimeOffset SignalledAt { get; set; }
        public bool ForceReleased { get; set; }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "DEADLOCK detected ({Length} sessions): {Cycle}. Victim: {Victim}")]
    private partial void LogDeadlock(int length, string cycle, string victim);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Victim {Victim} unresponsive; force-released {Count} lock(s)")]
    private partial void LogForceRelease(string victim, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deadlock {Cycle} resolved in {ElapsedMs:F0} ms")]
    private partial void LogResolved(string cycle, double elapsedMs);
}

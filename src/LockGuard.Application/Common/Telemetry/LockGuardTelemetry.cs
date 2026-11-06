using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace LockGuard.Application.Common.Telemetry;

/// <summary>
/// Metrics + traces emitted by LockGuard. Instrument names follow OpenTelemetry semantic conventions;
/// the Prometheus exporter turns <c>lockguard.lock.wait.duration</c> (unit <c>s</c>) into
/// <c>lockguard_lock_wait_duration_seconds_bucket</c>, counters get a <c>_total</c> suffix.
/// <para>
/// Resource names such as <c>order:42</c> are high-cardinality, so metrics are tagged with the
/// <em>resource group</em> (the prefix before the first ':') instead of the full resource name.
/// </para>
/// </summary>
public sealed class LockGuardTelemetry : IDisposable
{
    public const string MeterName = "LockGuard";
    public const string ActivitySourceName = "LockGuard";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    private readonly Meter _meter;
    private readonly bool _ownsMeter;
    private long _waitGraphEdges;
    private long _waitGraphCycles;
    private long _locksTracked;

    public LockGuardTelemetry(IMeterFactory? meterFactory = null)
    {
        // A meter created by IMeterFactory is owned (and disposed) by the factory.
        _ownsMeter = meterFactory is null;
        _meter = meterFactory?.Create(MeterName, "1.0.0") ?? new Meter(MeterName, "1.0.0");

        Acquisitions = _meter.CreateCounter<long>("lockguard.lock.acquisitions", "{acquisition}",
            "Lock acquisition outcomes (acquired, reentered, already_owned, timeout, deadlock_victim, cancelled).");
        ContentionEvents = _meter.CreateCounter<long>("lockguard.lock.contention", "{attempt}",
            "Acquisition attempts that found the lock held by another session.");
        WaitDuration = _meter.CreateHistogram<double>("lockguard.lock.wait.duration", "s",
            "Time spent waiting to acquire a lock.",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30] });
        HoldDuration = _meter.CreateHistogram<double>("lockguard.lock.hold.duration", "s",
            "Time a lock was held before release.",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.005, 0.01, 0.05, 0.1, 0.5, 1, 2.5, 5, 10, 30, 60, 300] });
        HeldLocks = _meter.CreateUpDownCounter<long>("lockguard.lock.held", "{lock}",
            "Locks currently held by this process.");
        WatchdogExtensions = _meter.CreateCounter<long>("lockguard.watchdog.extensions", "{extension}",
            "Watchdog lease renewals (result=extended|lost|error).");
        DeadlocksDetected = _meter.CreateCounter<long>("lockguard.deadlocks.detected", "{deadlock}",
            "Confirmed wait-for cycles.");
        DeadlocksResolved = _meter.CreateCounter<long>("lockguard.deadlocks.resolved", "{deadlock}",
            "Deadlocks resolved (strategy=victim_signal|force_release).");
        LocksReclaimed = _meter.CreateCounter<long>("lockguard.locks.reclaimed", "{lock}",
            "Stale / orphaned locks force-released by the reclaimer (reason=...).");

        _meter.CreateObservableGauge("lockguard.waitgraph.edges", () => Volatile.Read(ref _waitGraphEdges), "{edge}",
            "Edges of the Wait-For Graph observed in the last detector scan.");
        _meter.CreateObservableGauge("lockguard.waitgraph.cycles", () => Volatile.Read(ref _waitGraphCycles), "{cycle}",
            "Cycles of the Wait-For Graph observed in the last detector scan.");
        _meter.CreateObservableGauge("lockguard.locks.tracked", () => Volatile.Read(ref _locksTracked), "{lock}",
            "Locks present in the store during the last reclaimer scan.");
    }

    public Counter<long> Acquisitions { get; }
    public Counter<long> ContentionEvents { get; }
    public Histogram<double> WaitDuration { get; }
    public Histogram<double> HoldDuration { get; }
    public UpDownCounter<long> HeldLocks { get; }
    public Counter<long> WatchdogExtensions { get; }
    public Counter<long> DeadlocksDetected { get; }
    public Counter<long> DeadlocksResolved { get; }
    public Counter<long> LocksReclaimed { get; }

    public void SetWaitGraph(int edges, int cycles)
    {
        Volatile.Write(ref _waitGraphEdges, edges);
        Volatile.Write(ref _waitGraphCycles, cycles);
    }

    public void SetTrackedLocks(int count) => Volatile.Write(ref _locksTracked, count);

    public void Dispose()
    {
        if (_ownsMeter)
        {
            _meter.Dispose();
        }
    }
}

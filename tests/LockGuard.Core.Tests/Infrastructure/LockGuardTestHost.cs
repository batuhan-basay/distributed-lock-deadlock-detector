using LockGuard.Core;
using LockGuard.Core.Detection;
using LockGuard.Core.Diagnostics;
using LockGuard.Core.InMemory;
using LockGuard.Core.Locking;
using LockGuard.Core.Reclaim;
using LockGuard.Core.Watchdog;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LockGuard.Core.Tests.Infrastructure;

/// <summary>Wires the real LockGuard components on top of the in-memory stores (no DI container, no host).</summary>
internal sealed class LockGuardTestHost : IAsyncDisposable
{
    public LockGuardTestHost(TimeProvider? time = null, Action<LockGuardOptions>? configure = null,
        InMemoryLockStore? sharedStore = null, InMemoryCoordinationStore? sharedCoordination = null, string instanceId = "test-instance")
    {
        Time = time ?? TimeProvider.System;
        Options = new LockGuardOptions { ServiceName = "tests", InstanceId = instanceId };
        Options.Acquisition.InitialRetryDelay = TimeSpan.FromMilliseconds(2);
        Options.Acquisition.MaxRetryDelay = TimeSpan.FromMilliseconds(20);
        Options.Acquisition.WaitTimeout = TimeSpan.FromSeconds(10);
        Options.Detection.ConfirmationScans = 2;
        Options.Detection.ScanInterval = TimeSpan.FromMilliseconds(50);
        configure?.Invoke(Options);

        var options = Microsoft.Extensions.Options.Options.Create(Options);
        Store = sharedStore ?? new InMemoryLockStore(Time);
        Coordination = sharedCoordination ?? new InMemoryCoordinationStore(Time);
        Telemetry = new LockGuardTelemetry();
        Watchdog = new LockWatchdog(Store, Telemetry, Time, options, NullLogger<LockWatchdog>.Instance);
        Manager = new DistributedLockManager(Store, Coordination, Watchdog, Telemetry, Time, options, NullLogger<DistributedLockManager>.Instance);
        Detector = new DeadlockDetector(Store, Coordination, new DefaultDeadlockVictimSelector(), Telemetry, Time, options, NullLogger<DeadlockDetector>.Instance);
        Reclaimer = new OrphanedLockReclaimer(Store, Coordination, Telemetry, Time, options, NullLogger<OrphanedLockReclaimer>.Instance);
    }

    public TimeProvider Time { get; }
    public LockGuardOptions Options { get; }
    public InMemoryLockStore Store { get; }
    public InMemoryCoordinationStore Coordination { get; }
    public LockGuardTelemetry Telemetry { get; }
    public LockWatchdog Watchdog { get; }
    public DistributedLockManager Manager { get; }
    public DeadlockDetector Detector { get; }
    public OrphanedLockReclaimer Reclaimer { get; }

    /// <summary>Runs the detector in a background loop (what DeadlockDetectorService does in production).</summary>
    public async Task RunDetectorAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Detector.ScanAsync(cancellationToken);
                await Task.Delay(Options.Detection.ScanInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public ValueTask DisposeAsync()
    {
        Watchdog.Dispose();
        Telemetry.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>Deterministic clock for watchdog / reclaimer tests.</summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public ManualTimeProvider() : this(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero))
    {
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

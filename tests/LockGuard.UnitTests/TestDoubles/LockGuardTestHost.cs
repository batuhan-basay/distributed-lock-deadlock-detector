using LockGuard.Application.Common.Options;
using LockGuard.Application.Common.Telemetry;
using LockGuard.Application.Detection;
using LockGuard.Application.Locking;
using LockGuard.Application.Locking.Watchdog;
using LockGuard.Application.Reclamation;
using LockGuard.Domain.Deadlocks;
using LockGuard.Infrastructure.Persistence.InMemory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LockGuard.UnitTests.TestDoubles;

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
        Telemetry.Dispose();
        return ValueTask.CompletedTask;
    }
}

using LockGuard.Application.Abstractions.Locking;
using LockGuard.Application.Locking;
using LockGuard.Application.Locking.Watchdog;
using LockGuard.Domain.Coordination;
using LockGuard.Domain.Events;
using LockGuard.Domain.Exceptions;
using LockGuard.Domain.Locks;
using LockGuard.UnitTests.TestDoubles;

namespace LockGuard.UnitTests.Application.Detection;

public class DeadlockDetectionTests
{
    private static async Task<string> TransferAsync(IDistributedLockManager locks, string first, string second, TaskCompletionSource bothHoldFirstLock, Func<Task> onFirstAcquired)
    {
        await using var session = await locks.BeginSessionAsync(new LockSessionOptions { Name = $"{first}->{second}" });
        await using var a = await session.AcquireAsync(first);
        await onFirstAcquired();
        await bothHoldFirstLock.Task;
        try
        {
            await using var b = await session.AcquireAsync(second);
            return "completed";
        }
        catch (DeadlockVictimException)
        {
            return "victim";
        }
    }

    [Fact]
    public async Task Opposite_order_transfers_deadlock_and_exactly_one_victim_is_aborted()
    {
        await using var host = new LockGuardTestHost();
        using var cts = new CancellationTokenSource();
        var detector = host.RunDetectorAsync(cts.Token);

        var ready = 0;
        var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task Arrived()
        {
            if (Interlocked.Increment(ref ready) == 2)
            {
                both.SetResult();
            }

            return Task.CompletedTask;
        }

        var results = await Task.WhenAll(
            TransferAsync(host.Manager, "account:A", "account:B", both, Arrived),
            TransferAsync(host.Manager, "account:B", "account:A", both, Arrived));

        Assert.Equal(1, results.Count(r => r == "victim"));
        Assert.Equal(1, results.Count(r => r == "completed"));

        await Task.Delay(300);
        await cts.CancelAsync();
        await detector;

        var events = await host.Coordination.GetRecentEventsAsync(10);
        var detected = Assert.Single(events, e => e.Kind == LockEventKind.DeadlockDetected);
        Assert.Equal(2, detected.Cycle!.Count);
        Assert.Contains(events, e => e.Kind == LockEventKind.DeadlockResolved);
        Assert.Empty(await host.Store.ListAsync());
    }

    [Fact]
    public async Task Three_way_cycle_is_broken_by_a_single_victim()
    {
        await using var host = new LockGuardTestHost();
        using var cts = new CancellationTokenSource();
        var detector = host.RunDetectorAsync(cts.Token);

        string[] resources = ["ledger:1", "ledger:2", "ledger:3"];
        var ready = 0;
        var all = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task Arrived()
        {
            if (Interlocked.Increment(ref ready) == 3)
            {
                all.SetResult();
            }

            return Task.CompletedTask;
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 3)
            .Select(i => TransferAsync(host.Manager, resources[i], resources[(i + 1) % 3], all, Arrived)));

        await cts.CancelAsync();
        await detector;

        Assert.Equal(1, results.Count(r => r == "victim"));
        Assert.Equal(2, results.Count(r => r == "completed"));
        var detected = Assert.Single(await host.Coordination.GetRecentEventsAsync(10), e => e.Kind == LockEventKind.DeadlockDetected);
        Assert.Equal(3, detected.Cycle!.Count);
    }

    [Fact]
    public async Task A_cycle_is_only_acted_upon_after_the_confirmation_scans()
    {
        await using var host = new LockGuardTestHost(configure: o => o.Detection.ConfirmationScans = 3);
        var now = host.Time.GetUtcNow();

        host.Store.Seed("A", new LockOwner("g1", "svc", "i"), now, TimeSpan.FromMinutes(1));
        host.Store.Seed("B", new LockOwner("g2", "svc", "i"), now, TimeSpan.FromMinutes(1));
        await host.Coordination.RecordWaitAsync(new WaitRecord("g1", "B", "g2", "svc", "i", now, now));
        await host.Coordination.RecordWaitAsync(new WaitRecord("g2", "A", "g1", "svc", "i", now, now));

        await host.Detector.ScanAsync();
        await host.Detector.ScanAsync();
        Assert.Null(await host.Coordination.GetAbortSignalAsync("g1"));
        Assert.Null(await host.Coordination.GetAbortSignalAsync("g2"));

        await host.Detector.ScanAsync();
        var signalled = new[] { await host.Coordination.GetAbortSignalAsync("g1"), await host.Coordination.GetAbortSignalAsync("g2") };
        var signal = Assert.Single(signalled, s => s is not null);
        Assert.Equal(AbortReason.DeadlockVictim, signal!.Reason);
    }

    [Fact]
    public async Task Unresponsive_victim_is_force_released_so_the_survivor_can_proceed()
    {
        await using var host = new LockGuardTestHost(configure: o =>
        {
            o.Detection.VictimForceReleaseAfter = TimeSpan.FromMilliseconds(200);
            o.Acquisition.WaitTimeout = TimeSpan.FromSeconds(5);
        });
        using var cts = new CancellationTokenSource();
        var detector = host.RunDetectorAsync(cts.Token);

        var ghost = new LockOwner("ghost", "legacy-svc", "hung-instance");
        host.Store.Seed("A", ghost, host.Time.GetUtcNow(), TimeSpan.FromMinutes(5));
        using var refresher = new CancellationTokenSource();
        var keepGhostWaiting = Task.Run(async () =>
        {
            var since = host.Time.GetUtcNow();
            while (!refresher.IsCancellationRequested)
            {
                await host.Coordination.RecordWaitAsync(new WaitRecord("ghost", "B", null, "legacy-svc", "hung-instance", since, host.Time.GetUtcNow()));
                await Task.Delay(50);
            }
        });

        await using var healthy = await host.Manager.BeginSessionAsync(new LockSessionOptions { Priority = 10 });
        await using var b = await healthy.AcquireAsync("B");
        await using var a = await healthy.AcquireAsync("A"); 

        await refresher.CancelAsync();
        await keepGhostWaiting;
        await cts.CancelAsync();
        await detector;

        var events = await host.Coordination.GetRecentEventsAsync(20);
        Assert.Contains(events, e => e.Kind == LockEventKind.DeadlockDetected && e.VictimSessionId == "ghost");
        Assert.Contains(events, e => e.Kind == LockEventKind.VictimForceReleased && e.VictimSessionId == "ghost");
    }

    [Fact]
    public async Task Stale_wait_records_do_not_create_phantom_deadlocks()
    {
        await using var host = new LockGuardTestHost(configure: o => o.Detection.ConfirmationScans = 1);
        var now = host.Time.GetUtcNow();
        var old = now - TimeSpan.FromMinutes(1); 

        host.Store.Seed("A", new LockOwner("g1", "svc", "i"), now, TimeSpan.FromMinutes(1));
        host.Store.Seed("B", new LockOwner("g2", "svc", "i"), now, TimeSpan.FromMinutes(1));
        await host.Coordination.RecordWaitAsync(new WaitRecord("g1", "B", "g2", "svc", "i", old, old));
        await host.Coordination.RecordWaitAsync(new WaitRecord("g2", "A", "g1", "svc", "i", old, old));

        var snapshot = await host.Detector.ScanAsync();

        Assert.Empty(snapshot.Cycles);
        Assert.Empty(await host.Coordination.GetRecentEventsAsync(10));
    }
}

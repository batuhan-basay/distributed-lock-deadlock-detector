using LockGuard.Application.Features.Locks.Queries.GetLockEvents;
using LockGuard.Application.Features.Locks.Queries.GetLocks;
using LockGuard.Application.Features.Maintenance.Commands.ReclaimStaleLocks;
using LockGuard.Application.Features.Simulations.Commands.RunContention;
using LockGuard.Application.Features.Simulations.Commands.RunLongRunningJob;
using LockGuard.Application.Features.Simulations.Commands.RunReentrantScenario;
using LockGuard.Application.Features.Simulations.Commands.RunThreeWayDeadlock;
using LockGuard.Application.Features.Simulations.Commands.SimulateCrashedOwner;
using LockGuard.Application.Features.Simulations.Common;
using LockGuard.Domain.Events;
using LockGuard.UnitTests.TestDoubles;

namespace LockGuard.UnitTests.Application.Features.Simulations;

public class SimulationCommandHandlerTests
{
    [Fact]
    public async Task Three_way_deadlock_has_exactly_one_victim()
    {
        await using var host = new MediatorTestHost();
        using var cts = new CancellationTokenSource();
        var detector = host.RunDetectorAsync(cts.Token);

        var response = await host.SendAsync(new RunThreeWayDeadlockCommand(HoldMs: 300));

        await cts.CancelAsync();
        await detector;
        Assert.Equal(1, response.Victims);
        Assert.Equal(2, response.Completed);
        Assert.Equal(3, response.Workers.Count);
    }

    [Fact]
    public async Task Crashed_owner_lock_is_reclaimed_by_the_reclaim_command()
    {
        await using var host = new MediatorTestHost(o => o.Reclaimer.MinLockAge = TimeSpan.Zero);

        var crash = await host.SendAsync(new SimulateCrashedOwnerCommand("payment:tx-1", TtlSeconds: 600));
        Assert.Equal(ScenarioOutcome.Accepted, crash.Outcome);
        Assert.Single(await host.SendAsync(new GetLocksQuery("payment:")));

        var reclaimed = Assert.Single(await host.SendAsync(new ReclaimStaleLocksCommand()));

        Assert.Equal("payment:tx-1", reclaimed.Resource);
        Assert.Equal(crash.OrphanSessionId, reclaimed.PreviousOwnerSessionId);
        Assert.Empty(await host.SendAsync(new GetLocksQuery("payment:")));
        Assert.Single(await host.SendAsync(new GetLockEventsQuery(10, LockEventKind.OrphanedLockReclaimed)));
    }

    [Fact]
    public async Task Crash_simulation_on_a_busy_resource_reports_resource_busy()
    {
        await using var host = new MediatorTestHost();
        await host.SendAsync(new SimulateCrashedOwnerCommand("payment:tx-2"));

        var second = await host.SendAsync(new SimulateCrashedOwnerCommand("payment:tx-2"));

        Assert.Equal(ScenarioOutcome.ResourceBusy, second.Outcome);
        Assert.NotNull(second.HolderSessionId);
    }

    [Fact]
    public async Task Reentrant_scenario_hits_the_store_only_once()
    {
        await using var host = new MediatorTestHost();

        var response = await host.SendAsync(new RunReentrantScenarioCommand("order:7", Depth: 3));

        Assert.Equal(3, response.HoldCount);
        Assert.Equal(new[] { false, true, true }, response.Acquisitions.Select(a => a.IsReentrant));
        Assert.Empty(await host.SendAsync(new GetLocksQuery("order:")));
    }

    [Fact]
    public async Task Contention_requests_all_complete_and_report_their_wait()
    {
        await using var host = new MediatorTestHost();

        var responses = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => host.SendAsync(new RunContentionCommand("inventory:sku-1", WorkMs: 5))));

        Assert.All(responses, r => Assert.Equal(ScenarioOutcome.Completed, r.Outcome));
        Assert.Equal(20, responses.Select(r => r.FencingToken).Distinct().Count());
    }

    [Fact]
    public async Task Contention_request_times_out_when_the_hot_key_is_held()
    {
        await using var host = new MediatorTestHost();
        var holder = host.SendAsync(new RunContentionCommand("inventory:sku-9", WorkMs: 600));
        await Task.Delay(50);

        var waiter = await host.SendAsync(new RunContentionCommand("inventory:sku-9", WorkMs: 0, WaitTimeoutMs: 100));

        Assert.Equal(ScenarioOutcome.Timeout, waiter.Outcome);
        Assert.Equal((await holder).SessionId, waiter.HolderSessionId);
    }

    [Fact]
    public async Task Long_running_job_without_watchdog_reports_lock_lost()
    {
        await using var host = new MediatorTestHost();

        var response = await host.SendAsync(new RunLongRunningJobCommand("report:x", WorkMs: 700, LeaseMs: 500, DisableWatchdog: true));

        Assert.Equal(ScenarioOutcome.LockLost, response.Outcome);
        Assert.False(response.WatchdogEnabled);
        Assert.NotNull(response.Hint);
    }
}

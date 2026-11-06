using LockGuard.Application.Features.Locks.Queries.GetLockEvents;
using LockGuard.Application.Features.Simulations.Commands.RunDeadlockScenario;
using LockGuard.Application.Features.Simulations.Commands.RunOrderedTransfer;
using LockGuard.Application.Features.Simulations.Common;
using LockGuard.Domain.Events;
using LockGuard.UnitTests.TestDoubles;

namespace LockGuard.UnitTests.Application.Features.Simulations;

/// <summary>Scenario A and B sent through MediatR at the same time, exactly like the two HTTP endpoints.</summary>
public class RunDeadlockScenarioCommandHandlerTests
{
    [Fact]
    public async Task Scenario_a_and_b_together_end_with_one_victim_and_one_completed_transfer()
    {
        await using var host = new MediatorTestHost();
        using var cts = new CancellationTokenSource();
        var detector = host.RunDetectorAsync(cts.Token);

        var responses = await Task.WhenAll(
            host.SendAsync(RunDeadlockScenarioCommand.ScenarioA(holdMs: 300, workMs: 20, maxRetries: 0, sessionId: null)),
            host.SendAsync(RunDeadlockScenarioCommand.ScenarioB(holdMs: 300, workMs: 20, maxRetries: 0, sessionId: null)));

        await cts.CancelAsync();
        await detector;

        var victim = Assert.Single(responses, r => r.Outcome == ScenarioOutcome.DeadlockVictim);
        var winner = Assert.Single(responses, r => r.Outcome == ScenarioOutcome.Completed);
        Assert.True(victim.Retryable);
        Assert.Contains("deadlock cycle", victim.Detail);
        Assert.Equal(2, winner.LockOrder.Count);

        var detected = await host.SendAsync(new GetLockEventsQuery(10, LockEventKind.DeadlockDetected));
        Assert.Equal(victim.SessionId, Assert.Single(detected).VictimSessionId);
    }

    [Fact]
    public async Task With_retries_the_victim_redoes_its_work_and_both_transfers_complete()
    {
        await using var host = new MediatorTestHost();
        using var cts = new CancellationTokenSource();
        var detector = host.RunDetectorAsync(cts.Token);

        var responses = await Task.WhenAll(
            host.SendAsync(RunDeadlockScenarioCommand.ScenarioA(holdMs: 300, workMs: 20, maxRetries: 2, sessionId: null)),
            host.SendAsync(RunDeadlockScenarioCommand.ScenarioB(holdMs: 300, workMs: 20, maxRetries: 2, sessionId: null)));

        await cts.CancelAsync();
        await detector;

        Assert.All(responses, r => Assert.Equal(ScenarioOutcome.Completed, r.Outcome));
        var retried = Assert.Single(responses, r => r.Attempts > 1);
        Assert.Equal(ScenarioOutcome.DeadlockVictim, retried.PreviousAttempts[0].Outcome);
    }

    [Fact]
    public async Task Ordered_transfers_in_opposite_order_never_deadlock_even_without_detector()
    {
        await using var host = new MediatorTestHost(o => o.Acquisition.WaitTimeout = TimeSpan.FromSeconds(5));

        var responses = await Task.WhenAll(
            host.SendAsync(new RunOrderedTransferCommand("account:A", "account:B", WorkMs: 50)),
            host.SendAsync(new RunOrderedTransferCommand("account:B", "account:A", WorkMs: 50)));

        Assert.All(responses, r =>
        {
            Assert.Equal(ScenarioOutcome.Completed, r.Outcome);
            Assert.Equal(new[] { "account:A", "account:B" }, r.LockOrder.Select(l => l.Resource));
        });
    }
}

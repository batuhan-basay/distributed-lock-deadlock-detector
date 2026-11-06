using FluentValidation;
using LockGuard.Application.Features.Locks.Queries.GetLockEvents;
using LockGuard.Application.Features.Locks.Queries.GetLocks;
using LockGuard.Application.Features.Simulations.Commands.RunDeadlockScenario;
using LockGuard.UnitTests.TestDoubles;

namespace LockGuard.UnitTests.Application.Behaviors;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Invalid_command_is_rejected_before_the_handler_runs()
    {
        await using var host = new MediatorTestHost();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            host.SendAsync(new RunDeadlockScenarioCommand("bad", "account:A", "account:A", HoldMs: -1)));

        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(RunDeadlockScenarioCommand.SecondResource));
        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(RunDeadlockScenarioCommand.HoldMs));

        // The handler never ran: no lock was taken.
        Assert.Empty(await host.SendAsync(new GetLocksQuery()));
    }

    [Fact]
    public async Task Invalid_query_is_rejected()
    {
        await using var host = new MediatorTestHost();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new GetLockEventsQuery(Count: 0)));

        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(GetLockEventsQuery.Count));
    }

    [Fact]
    public async Task Valid_request_reaches_its_handler()
    {
        await using var host = new MediatorTestHost();

        var events = await host.SendAsync(new GetLockEventsQuery(Count: 10));

        Assert.Empty(events);
    }
}

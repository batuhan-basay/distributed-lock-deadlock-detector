using LockGuard.Application.Features.Simulations.Commands.RunContention;
using LockGuard.Application.Features.Simulations.Commands.RunDeadlockScenario;
using LockGuard.Application.Features.Simulations.Commands.RunLongRunningJob;
using LockGuard.Application.Features.Simulations.Commands.RunOrderedTransfer;
using LockGuard.Application.Features.Simulations.Commands.RunReentrantScenario;
using LockGuard.Application.Features.Simulations.Commands.RunThreeWayDeadlock;
using LockGuard.Application.Features.Simulations.Commands.SimulateCrashedOwner;

namespace LockGuard.UnitTests.Application.Validators;

public class SimulationCommandValidatorTests
{
    [Fact]
    public void Default_scenario_a_and_b_commands_are_valid()
    {
        var validator = new RunDeadlockScenarioCommandValidator();

        Assert.True(validator.Validate(RunDeadlockScenarioCommand.ScenarioA(1500, 200, 0, null)).IsValid);
        Assert.True(validator.Validate(RunDeadlockScenarioCommand.ScenarioB(1500, 200, 2, "order-42:retry_1")).IsValid);
    }

    [Theory]
    [InlineData("account:A", "account:A", 100, 0, null, "SecondResource")]       // nothing to deadlock on
    [InlineData("", "account:B", 100, 0, null, "FirstResource")]
    [InlineData("lockguard:leader:x", "account:B", 100, 0, null, "FirstResource")] // internal resource
    [InlineData("account:A", "account:B", 60_000, 0, null, "HoldMs")]
    [InlineData("account:A", "account:B", 100, 9, null, "MaxRetries")]
    [InlineData("account:A", "account:B", 100, 0, "has spaces", "SessionId")]
    public void Invalid_deadlock_scenario_is_rejected(string first, string second, int holdMs, int maxRetries, string? sessionId, string property)
    {
        var result = new RunDeadlockScenarioCommandValidator()
            .Validate(new RunDeadlockScenarioCommand("scenario", first, second, holdMs, 100, maxRetries, sessionId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }

    [Fact]
    public void Ordered_transfer_requires_two_different_resources()
    {
        var validator = new RunOrderedTransferCommandValidator();

        Assert.True(validator.Validate(new RunOrderedTransferCommand("account:B", "account:A")).IsValid);
        Assert.False(validator.Validate(new RunOrderedTransferCommand("account:A", "account:A")).IsValid);
    }

    [Theory]
    [InlineData(99, false)]
    [InlineData(100, true)]
    [InlineData(10_000, true)]
    [InlineData(10_001, false)]
    public void Three_way_hold_time_must_allow_all_sessions_to_hold_their_first_lock(int holdMs, bool valid) =>
        Assert.Equal(valid, new RunThreeWayDeadlockCommandValidator().Validate(new RunThreeWayDeadlockCommand(holdMs)).IsValid);

    [Theory]
    [InlineData(25, 10_000, true)]
    [InlineData(-1, 10_000, false)]
    [InlineData(25, 5, false)]
    public void Contention_bounds(int workMs, int waitTimeoutMs, bool valid) =>
        Assert.Equal(valid, new RunContentionCommandValidator().Validate(new RunContentionCommand("inventory:sku-1", workMs, waitTimeoutMs)).IsValid);

    [Theory]
    [InlineData(20_000, 3_000, true)]
    [InlineData(20_000, 100, false)]
    [InlineData(400_000, 3_000, false)]
    public void Long_running_job_bounds(int workMs, int leaseMs, bool valid) =>
        Assert.Equal(valid, new RunLongRunningJobCommandValidator().Validate(new RunLongRunningJobCommand("report:monthly", workMs, leaseMs)).IsValid);

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(100_000, false)]
    public void Crash_simulation_ttl_bounds(int ttlSeconds, bool valid) =>
        Assert.Equal(valid, new SimulateCrashedOwnerCommandValidator().Validate(new SimulateCrashedOwnerCommand("payment:1", ttlSeconds)).IsValid);

    [Theory]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(0, false)]
    [InlineData(11, false)]
    public void Reentrant_depth_bounds(int depth, bool valid) =>
        Assert.Equal(valid, new RunReentrantScenarioCommandValidator().Validate(new RunReentrantScenarioCommand("order:1", depth)).IsValid);
}

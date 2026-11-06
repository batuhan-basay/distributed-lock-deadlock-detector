using LockGuard.Application.Features.Simulations.Common;

namespace LockGuard.Application.Features.Simulations.Commands.RunThreeWayDeadlock;

public sealed record ThreeWayDeadlockResponse(
    int Victims,
    int Completed,
    IReadOnlyList<ThreeWayWorkerResult> Workers) : IScenarioResponse
{
    
    public ScenarioOutcome Outcome => ScenarioOutcome.Completed;

    public string Scenario { get; } = "three-way-deadlock";
}

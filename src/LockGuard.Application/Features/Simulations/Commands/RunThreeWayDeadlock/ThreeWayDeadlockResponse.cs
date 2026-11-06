using LockGuard.Application.Features.Simulations.Common;

namespace LockGuard.Application.Features.Simulations.Commands.RunThreeWayDeadlock;

public sealed record ThreeWayDeadlockResponse(
    int Victims,
    int Completed,
    IReadOnlyList<ThreeWayWorkerResult> Workers) : IScenarioResponse
{
    /// <summary>The scenario itself always completes; the per-worker outcomes tell who was the victim.</summary>
    public ScenarioOutcome Outcome => ScenarioOutcome.Completed;

    public string Scenario { get; } = "three-way-deadlock";
}

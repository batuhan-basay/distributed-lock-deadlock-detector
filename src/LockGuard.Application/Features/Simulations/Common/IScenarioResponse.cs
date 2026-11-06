namespace LockGuard.Application.Features.Simulations.Common;

/// <summary>Every simulation response carries its <see cref="ScenarioOutcome"/>.</summary>
public interface IScenarioResponse
{
    ScenarioOutcome Outcome { get; }
}

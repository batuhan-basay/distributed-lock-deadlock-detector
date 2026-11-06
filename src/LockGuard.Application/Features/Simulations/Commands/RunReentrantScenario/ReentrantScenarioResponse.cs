using LockGuard.Application.Features.Simulations.Common;

namespace LockGuard.Application.Features.Simulations.Commands.RunReentrantScenario;

public sealed record ReentrantScenarioResponse(
    ScenarioOutcome Outcome,
    string Resource,
    string SessionId,
    int HoldCount,
    IReadOnlyList<ReentrantAcquisitionDto> Acquisitions) : IScenarioResponse;

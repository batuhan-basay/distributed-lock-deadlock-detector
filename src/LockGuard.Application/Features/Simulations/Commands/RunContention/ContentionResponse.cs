using LockGuard.Application.Features.Simulations.Common;

namespace LockGuard.Application.Features.Simulations.Commands.RunContention;

public sealed record ContentionResponse(
    ScenarioOutcome Outcome,
    string Resource,
    string SessionId,
    long WaitedMs,
    long TotalMs,
    long? FencingToken = null,
    string? HolderSessionId = null) : IScenarioResponse;

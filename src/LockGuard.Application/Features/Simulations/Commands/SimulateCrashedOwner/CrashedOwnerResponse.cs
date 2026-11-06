using LockGuard.Application.Features.Simulations.Common;

namespace LockGuard.Application.Features.Simulations.Commands.SimulateCrashedOwner;

public sealed record CrashedOwnerResponse(
    ScenarioOutcome Outcome,
    string Resource,
    int TtlSeconds,
    string? OrphanSessionId = null,
    string? DeadInstanceId = null,
    string? ExpectedReclaimWithin = null,
    string? HolderSessionId = null) : IScenarioResponse;

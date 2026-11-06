using LockGuard.Application.Features.Simulations.Common;

namespace LockGuard.Application.Features.Simulations.Commands.RunThreeWayDeadlock;

public sealed record ThreeWayWorkerResult(
    int Worker,
    string SessionId,
    string Path,
    ScenarioOutcome Outcome,
    long ElapsedMs,
    string? Detail = null);

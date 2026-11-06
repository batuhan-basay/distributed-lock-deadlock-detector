using LockGuard.Application.Features.Simulations.Common;

namespace LockGuard.Application.Features.Simulations.Commands.RunLongRunningJob;

public sealed record LongRunningJobResponse(
    ScenarioOutcome Outcome,
    string Resource,
    string SessionId,
    int LeaseMs,
    int WorkMs,
    bool WatchdogEnabled,
    int ExpectedExtensions,
    long ElapsedMs,
    string? Reason = null,
    string? Hint = null) : IScenarioResponse;

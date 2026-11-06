namespace LockGuard.Application.Features.Simulations.Common;

public sealed record FailedAttemptDto(string SessionId, ScenarioOutcome Outcome, string Detail);

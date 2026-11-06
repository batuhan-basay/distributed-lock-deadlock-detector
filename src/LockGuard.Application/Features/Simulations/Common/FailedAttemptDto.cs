namespace LockGuard.Application.Features.Simulations.Common;

/// <summary>An attempt that was aborted before the unit of work was retried.</summary>
public sealed record FailedAttemptDto(string SessionId, ScenarioOutcome Outcome, string Detail);

namespace LockGuard.Application.Features.Simulations.Common;

/// <summary>Result of a two-resource "transfer" (deadlock scenarios A/B and the ordered/safe variant).</summary>
public sealed record TransferResponse(
    ScenarioOutcome Outcome,
    string Scenario,
    string SessionId,
    int Attempts,
    IReadOnlyList<FailedAttemptDto> PreviousAttempts,
    IReadOnlyList<AcquiredLockDto> LockOrder,
    long ElapsedMs,
    string? Detail = null,
    string? HolderSessionId = null) : IScenarioResponse
{
    /// <summary>A deadlock victim can simply retry the whole unit of work.</summary>
    public bool Retryable => Outcome == ScenarioOutcome.DeadlockVictim;
}

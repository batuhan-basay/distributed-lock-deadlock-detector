namespace LockGuard.Application.Features.Simulations.Common;

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
    
    public bool Retryable => Outcome == ScenarioOutcome.DeadlockVictim;
}

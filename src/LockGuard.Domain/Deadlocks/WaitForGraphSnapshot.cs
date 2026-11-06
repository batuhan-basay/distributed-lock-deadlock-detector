namespace LockGuard.Domain.Deadlocks;

public sealed record WaitForGraphSnapshot(
    DateTimeOffset CapturedAt,
    IReadOnlyList<WaitEdge> Edges,
    IReadOnlyList<DeadlockCycle> Cycles)
{
    public bool IsDeadlocked => Cycles.Count > 0;
}

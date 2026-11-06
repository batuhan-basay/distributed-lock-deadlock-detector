namespace LockGuard.Domain.Deadlocks;

/// <summary>Snapshot of the whole Wait-For Graph at a point in time.</summary>
public sealed record WaitForGraphSnapshot(
    DateTimeOffset CapturedAt,
    IReadOnlyList<WaitEdge> Edges,
    IReadOnlyList<DeadlockCycle> Cycles)
{
    public bool IsDeadlocked => Cycles.Count > 0;
}

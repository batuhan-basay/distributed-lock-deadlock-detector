using LockGuard.Domain.Deadlocks;

namespace LockGuard.Application.Features.Locks.Queries.GetWaitForGraph;

public sealed record WaitForGraphResponse(
    DateTimeOffset CapturedAt,
    bool Deadlocked,
    IReadOnlyList<WaitEdge> Edges,
    IReadOnlyList<CycleDto> Cycles,
    string Mermaid);

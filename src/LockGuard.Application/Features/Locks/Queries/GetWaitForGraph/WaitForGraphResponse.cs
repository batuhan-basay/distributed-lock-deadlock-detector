using LockGuard.Domain.Deadlocks;

namespace LockGuard.Application.Features.Locks.Queries.GetWaitForGraph;

/// <param name="Mermaid">The graph as Mermaid text (paste into https://mermaid.live or a GitHub comment).</param>
public sealed record WaitForGraphResponse(
    DateTimeOffset CapturedAt,
    bool Deadlocked,
    IReadOnlyList<WaitEdge> Edges,
    IReadOnlyList<CycleDto> Cycles,
    string Mermaid);

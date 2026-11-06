using LockGuard.Domain.Deadlocks;

namespace LockGuard.Application.Features.Locks.Queries.GetWaitForGraph;

public sealed record CycleDto(IReadOnlyList<string> Sessions, string Description, IReadOnlyList<WaitEdge> Edges);

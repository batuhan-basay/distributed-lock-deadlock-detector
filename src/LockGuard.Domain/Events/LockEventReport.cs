using LockGuard.Domain.Deadlocks;

namespace LockGuard.Domain.Events;

public sealed record LockEventReport(
    string Id,
    LockEventKind Kind,
    DateTimeOffset OccurredAt,
    string Summary,
    string? VictimSessionId = null,
    IReadOnlyList<WaitEdge>? Cycle = null,
    string? Resource = null,
    string? PreviousOwnerSessionId = null,
    string? Reason = null,
    string? DetectedBy = null);

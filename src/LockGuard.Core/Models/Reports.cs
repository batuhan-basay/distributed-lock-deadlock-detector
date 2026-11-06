namespace LockGuard.Core.Models;

/// <summary>Directed edge of the Wait-For Graph: <c>Waiter --(Resource)--&gt; Holder</c>.</summary>
public sealed record WaitEdge(string WaiterSessionId, string HolderSessionId, string Resource, DateTimeOffset WaitingSince);

/// <summary>A cycle in the Wait-For Graph, i.e. a deadlock.</summary>
public sealed record DeadlockCycle(IReadOnlyList<string> Sessions, IReadOnlyList<WaitEdge> Edges)
{
    /// <summary>Stable, order-independent identity of the cycle, used for confirmation across scans.</summary>
    public string Signature { get; } = string.Join('|', Edges
        .Select(e => $"{e.WaiterSessionId}>{e.Resource}>{e.HolderSessionId}")
        .Order(StringComparer.Ordinal));

    /// <summary>Human readable form, e.g. <c>s1 -[account:B]-&gt; s2 -[account:A]-&gt; s1</c>.</summary>
    public string Describe()
    {
        if (Edges.Count == 0)
        {
            return string.Empty;
        }

        var parts = Edges.Select(e => $"{e.WaiterSessionId} -[{e.Resource}]->").ToList();
        parts.Add(Edges[0].WaiterSessionId);
        return string.Join(' ', parts);
    }
}

/// <summary>Snapshot of the whole Wait-For Graph, served by the diagnostics API.</summary>
public sealed record WaitForGraphSnapshot(
    DateTimeOffset CapturedAt,
    IReadOnlyList<WaitEdge> Edges,
    IReadOnlyList<DeadlockCycle> Cycles);

public enum LockEventKind
{
    DeadlockDetected,
    DeadlockResolved,
    VictimForceReleased,
    OrphanedLockReclaimed,
}

/// <summary>Audit record persisted in the coordination store and exposed by <c>GET /api/locks/events</c>.</summary>
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

namespace LockGuard.Domain.Deadlocks;

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

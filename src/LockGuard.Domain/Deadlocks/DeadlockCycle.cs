namespace LockGuard.Domain.Deadlocks;

public sealed record DeadlockCycle(IReadOnlyList<string> Sessions, IReadOnlyList<WaitEdge> Edges)
{
    
    public string Signature { get; } = string.Join('|', Edges
        .Select(e => $"{e.WaiterSessionId}>{e.Resource}>{e.HolderSessionId}")
        .Order(StringComparer.Ordinal));

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

using System.Text;
using LockGuard.Domain.Deadlocks;

namespace LockGuard.Application.Features.Locks.Queries.GetWaitForGraph;

public static class WaitForGraphMermaidRenderer
{
    public static string Render(WaitForGraphSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        string Id(string session)
        {
            if (!ids.TryGetValue(session, out var id))
            {
                ids[session] = id = $"n{ids.Count}";
            }

            return id;
        }

        var sb = new StringBuilder("graph LR\n");
        foreach (var edge in snapshot.Edges)
        {
            sb.Append($"  {Id(edge.WaiterSessionId)}[\"{edge.WaiterSessionId}\"] -- \"waits for {edge.Resource}\" --> ")
              .Append($"{Id(edge.HolderSessionId)}[\"{edge.HolderSessionId}\"]\n");
        }

        foreach (var session in snapshot.Cycles.SelectMany(c => c.Sessions).Distinct(StringComparer.Ordinal))
        {
            sb.Append($"  style {Id(session)} fill:#ffdddd,stroke:#cc0000\n");
        }

        return sb.ToString();
    }
}

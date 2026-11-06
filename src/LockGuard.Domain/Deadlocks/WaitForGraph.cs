using LockGuard.Domain.Coordination;

namespace LockGuard.Domain.Deadlocks;

public sealed class WaitForGraph
{
    private readonly Dictionary<string, List<WaitEdge>> _adjacency;

    private WaitForGraph(IReadOnlyList<WaitEdge> edges)
    {
        Edges = edges;
        _adjacency = new Dictionary<string, List<WaitEdge>>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            if (!_adjacency.TryGetValue(edge.WaiterSessionId, out var list))
            {
                _adjacency[edge.WaiterSessionId] = list = [];
            }

            list.Add(edge);
        }
    }

    public IReadOnlyList<WaitEdge> Edges { get; }

    public static WaitForGraph Build(IEnumerable<WaitRecord> waits, IReadOnlyDictionary<string, string> holderByResource)
    {
        var edges = new List<WaitEdge>();
        foreach (var wait in waits)
        {
            if (!holderByResource.TryGetValue(wait.Resource, out var holder))
            {
                continue; 
            }

            if (string.Equals(holder, wait.WaiterSessionId, StringComparison.Ordinal))
            {
                continue; 
            }

            edges.Add(new WaitEdge(wait.WaiterSessionId, holder, wait.Resource, wait.WaitingSince));
        }

        return new WaitForGraph(edges);
    }

    public static WaitForGraph FromEdges(IEnumerable<WaitEdge> edges) => new([.. edges]);

    public IReadOnlyList<DeadlockCycle> FindCycles()
    {
        var cycles = new List<DeadlockCycle>();
        foreach (var component in StronglyConnectedComponents())
        {
            if (component.Count < 2)
            {
                continue; 
            }

            var cycle = ExtractCycle(component);
            if (cycle is not null)
            {
                cycles.Add(cycle);
            }
        }

        return cycles;
    }

    private List<HashSet<string>> StronglyConnectedComponents()
    {
        var index = 0;
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        var lowLink = new Dictionary<string, int>(StringComparer.Ordinal);
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var components = new List<HashSet<string>>();

        var vertices = Edges.SelectMany(e => new[] { e.WaiterSessionId, e.HolderSessionId })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        foreach (var root in vertices)
        {
            if (indices.ContainsKey(root))
            {
                continue;
            }

            var callStack = new Stack<(string Vertex, int NextEdge)>();
            Visit(root);

            while (callStack.Count > 0)
            {
                var (v, next) = callStack.Pop();
                var successors = Successors(v);

                if (next < successors.Count)
                {
                    callStack.Push((v, next + 1));
                    var w = successors[next].HolderSessionId;
                    if (!indices.TryGetValue(w, out var wIndex))
                    {
                        Visit(w);
                    }
                    else if (onStack.Contains(w))
                    {
                        lowLink[v] = Math.Min(lowLink[v], wIndex);
                    }

                    continue;
                }

                if (lowLink[v] == indices[v])
                {
                    var component = new HashSet<string>(StringComparer.Ordinal);
                    string member;
                    do
                    {
                        member = stack.Pop();
                        onStack.Remove(member);
                        component.Add(member);
                    }
                    while (!string.Equals(member, v, StringComparison.Ordinal));

                    components.Add(component);
                }

                if (callStack.Count > 0)
                {
                    var parent = callStack.Peek().Vertex;
                    lowLink[parent] = Math.Min(lowLink[parent], lowLink[v]);
                }
            }

            void Visit(string vertex)
            {
                indices[vertex] = index;
                lowLink[vertex] = index;
                index++;
                stack.Push(vertex);
                onStack.Add(vertex);
                callStack.Push((vertex, 0));
            }
        }

        return components;
    }

    private DeadlockCycle? ExtractCycle(HashSet<string> component)
    {
        var start = component.Order(StringComparer.Ordinal).First();
        var parentEdge = new Dictionary<string, WaitEdge>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(start);
        var visited = new HashSet<string>(StringComparer.Ordinal) { start };

        while (queue.Count > 0)
        {
            var v = queue.Dequeue();
            foreach (var edge in Successors(v))
            {
                var w = edge.HolderSessionId;
                if (!component.Contains(w))
                {
                    continue;
                }

                if (string.Equals(w, start, StringComparison.Ordinal))
                {
                    
                    var path = new List<WaitEdge> { edge };
                    var cursor = v;
                    while (!string.Equals(cursor, start, StringComparison.Ordinal))
                    {
                        var e = parentEdge[cursor];
                        path.Add(e);
                        cursor = e.WaiterSessionId;
                    }

                    path.Reverse();
                    return new DeadlockCycle([.. path.Select(e => e.WaiterSessionId)], path);
                }

                if (visited.Add(w))
                {
                    parentEdge[w] = edge;
                    queue.Enqueue(w);
                }
            }
        }

        return null;
    }

    private List<WaitEdge> Successors(string vertex) =>
        _adjacency.TryGetValue(vertex, out var list) ? list : [];
}

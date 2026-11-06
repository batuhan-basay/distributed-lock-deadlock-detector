using LockGuard.Core.Detection;
using LockGuard.Core.Models;

namespace LockGuard.Core.Tests;

public class WaitForGraphTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static WaitRecord Wait(string waiter, string resource) =>
        new(waiter, resource, null, "svc", "inst", Now, Now);

    [Fact]
    public void Two_sessions_waiting_on_each_other_form_a_cycle()
    {
        // s1 holds A, waits for B; s2 holds B, waits for A.
        var holders = new Dictionary<string, string> { ["A"] = "s1", ["B"] = "s2" };
        var graph = WaitForGraph.Build([Wait("s1", "B"), Wait("s2", "A")], holders);

        var cycle = Assert.Single(graph.FindCycles());
        Assert.Equal(new[] { "s1", "s2" }, cycle.Sessions);
        Assert.Equal(2, cycle.Edges.Count);
        Assert.Equal("s1 -[B]-> s2 -[A]-> s1", cycle.Describe());
    }

    [Fact]
    public void Three_way_cycle_is_detected_with_every_edge()
    {
        var holders = new Dictionary<string, string> { ["L1"] = "s1", ["L2"] = "s2", ["L3"] = "s3" };
        var graph = WaitForGraph.Build([Wait("s1", "L2"), Wait("s2", "L3"), Wait("s3", "L1")], holders);

        var cycle = Assert.Single(graph.FindCycles());
        Assert.Equal(3, cycle.Sessions.Count);
        Assert.Equal(new[] { "L2", "L3", "L1" }, cycle.Edges.Select(e => e.Resource));
    }

    [Fact]
    public void Simple_chain_without_back_edge_is_not_a_deadlock()
    {
        // s1 -> s2 -> s3 (s3 is running, not waiting)
        var holders = new Dictionary<string, string> { ["B"] = "s2", ["C"] = "s3" };
        var graph = WaitForGraph.Build([Wait("s1", "B"), Wait("s2", "C")], holders);

        Assert.Equal(2, graph.Edges.Count);
        Assert.Empty(graph.FindCycles());
    }

    [Fact]
    public void Waits_on_free_resources_and_self_waits_are_ignored()
    {
        var holders = new Dictionary<string, string> { ["A"] = "s1" };
        var graph = WaitForGraph.Build([Wait("s1", "A"), Wait("s2", "FREE")], holders);

        Assert.Empty(graph.Edges);
        Assert.Empty(graph.FindCycles());
    }

    [Fact]
    public void Disjoint_cycles_are_reported_separately()
    {
        var holders = new Dictionary<string, string>
        {
            ["A"] = "s1", ["B"] = "s2",   // cycle 1
            ["X"] = "t1", ["Y"] = "t2",   // cycle 2
            ["Z"] = "t1",
        };
        var graph = WaitForGraph.Build(
            [Wait("s1", "B"), Wait("s2", "A"), Wait("t1", "Y"), Wait("t2", "X"), Wait("u1", "Z")],
            holders);

        var cycles = graph.FindCycles();
        Assert.Equal(2, cycles.Count);
        Assert.DoesNotContain(cycles, c => c.Sessions.Contains("u1")); // u1 waits on a deadlocked session but is not part of a cycle
    }

    [Fact]
    public void Cycle_signature_is_independent_of_discovery_order()
    {
        var holders = new Dictionary<string, string> { ["A"] = "s1", ["B"] = "s2" };
        var first = WaitForGraph.Build([Wait("s1", "B"), Wait("s2", "A")], holders).FindCycles()[0];
        var second = WaitForGraph.Build([Wait("s2", "A"), Wait("s1", "B")], holders).FindCycles()[0];

        Assert.Equal(first.Signature, second.Signature);
    }

    [Fact]
    public void Large_graph_with_long_cycle_is_handled_iteratively()
    {
        const int n = 5_000;
        var edges = Enumerable.Range(0, n)
            .Select(i => new WaitEdge($"s{i}", $"s{(i + 1) % n}", $"r{(i + 1) % n}", Now));

        var cycle = Assert.Single(WaitForGraph.FromEdges(edges).FindCycles());
        Assert.Equal(n, cycle.Sessions.Count);
    }
}

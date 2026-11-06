using LockGuard.Core.Detection;
using LockGuard.Core.Models;

namespace LockGuard.Core.Tests;

public class DeadlockVictimSelectorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly DefaultDeadlockVictimSelector _selector = new();

    private static DeadlockCycle Cycle(params string[] sessions) => new(sessions,
        [.. sessions.Select((s, i) => new WaitEdge(s, sessions[(i + 1) % sessions.Length], $"r{i}", T0))]);

    private static SessionInfo Session(string id, int priority = 0, int startedSecondsAfterT0 = 0) =>
        new(id, "svc", "inst", id, priority, T0.AddSeconds(startedSecondsAfterT0));

    [Fact]
    public void Lowest_priority_session_is_the_victim()
    {
        var sessions = new Dictionary<string, SessionInfo>
        {
            ["vip"] = Session("vip", priority: 10),
            ["batch"] = Session("batch", priority: -5),
        };

        Assert.Equal("batch", _selector.SelectVictim(Cycle("vip", "batch"), sessions, new Dictionary<string, int>()));
    }

    [Fact]
    public void With_equal_priority_the_session_holding_fewer_locks_is_the_victim()
    {
        var sessions = new Dictionary<string, SessionInfo> { ["a"] = Session("a"), ["b"] = Session("b") };
        var held = new Dictionary<string, int> { ["a"] = 5, ["b"] = 1 };

        Assert.Equal("b", _selector.SelectVictim(Cycle("a", "b"), sessions, held));
    }

    [Fact]
    public void With_equal_priority_and_locks_the_youngest_session_is_the_victim()
    {
        var sessions = new Dictionary<string, SessionInfo>
        {
            ["old"] = Session("old", startedSecondsAfterT0: 0),
            ["young"] = Session("young", startedSecondsAfterT0: 30),
        };

        Assert.Equal("young", _selector.SelectVictim(Cycle("old", "young"), sessions, new Dictionary<string, int>()));
    }

    [Fact]
    public void Session_without_metadata_is_preferred_as_victim()
    {
        var sessions = new Dictionary<string, SessionInfo> { ["known"] = Session("known", priority: -100) };

        Assert.Equal("orphan", _selector.SelectVictim(Cycle("known", "orphan"), sessions, new Dictionary<string, int>()));
    }

    [Fact]
    public void Decision_is_deterministic_for_identical_candidates()
    {
        var sessions = new Dictionary<string, SessionInfo> { ["s1"] = Session("s1"), ["s2"] = Session("s2") };
        var held = new Dictionary<string, int>();

        var first = _selector.SelectVictim(Cycle("s1", "s2"), sessions, held);
        var second = _selector.SelectVictim(Cycle("s2", "s1"), sessions, held);

        Assert.Equal(first, second);
    }
}

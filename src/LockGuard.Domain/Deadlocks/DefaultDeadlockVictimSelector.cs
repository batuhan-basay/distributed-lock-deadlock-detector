using LockGuard.Domain.Sessions;

namespace LockGuard.Domain.Deadlocks;

public sealed class DefaultDeadlockVictimSelector : IDeadlockVictimSelector
{
    public string SelectVictim(DeadlockCycle cycle, IReadOnlyDictionary<string, SessionInfo> sessions, IReadOnlyDictionary<string, int> heldLockCount)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        if (cycle.Sessions.Count == 0)
        {
            throw new ArgumentException("Cycle has no sessions.", nameof(cycle));
        }

        return cycle.Sessions
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => sessions.ContainsKey(s) ? 1 : 0)
            .ThenBy(s => sessions.TryGetValue(s, out var info) ? info.Priority : int.MinValue)
            .ThenBy(s => heldLockCount.TryGetValue(s, out var n) ? n : 0)
            .ThenByDescending(s => sessions.TryGetValue(s, out var info) ? info.StartedAt : DateTimeOffset.MaxValue)
            .ThenByDescending(s => s, StringComparer.Ordinal)
            .First();
    }
}

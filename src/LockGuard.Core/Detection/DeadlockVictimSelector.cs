using LockGuard.Core.Models;

namespace LockGuard.Core.Detection;

/// <summary>Chooses which session of a deadlock cycle gets aborted.</summary>
public interface IDeadlockVictimSelector
{
    string SelectVictim(DeadlockCycle cycle, IReadOnlyDictionary<string, SessionInfo> sessions, IReadOnlyDictionary<string, int> heldLockCount);
}

/// <summary>
/// Default policy, similar to what relational databases do:
/// <list type="number">
///   <item>lowest <see cref="SessionInfo.Priority"/> first (business-critical sessions survive);</item>
///   <item>then the session holding the fewest locks (least work to roll back);</item>
///   <item>then the youngest session (it has done the least work, and older sessions are not starved);</item>
///   <item>finally the ordinal-largest id, so the decision is deterministic across detector instances.</item>
/// </list>
/// Sessions without metadata (e.g. their process crashed) are preferred as victims.
/// </summary>
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

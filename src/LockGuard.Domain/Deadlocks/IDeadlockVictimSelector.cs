using LockGuard.Domain.Sessions;

namespace LockGuard.Domain.Deadlocks;

/// <summary>Chooses which session of a deadlock cycle gets aborted.</summary>
public interface IDeadlockVictimSelector
{
    string SelectVictim(DeadlockCycle cycle, IReadOnlyDictionary<string, SessionInfo> sessions, IReadOnlyDictionary<string, int> heldLockCount);
}

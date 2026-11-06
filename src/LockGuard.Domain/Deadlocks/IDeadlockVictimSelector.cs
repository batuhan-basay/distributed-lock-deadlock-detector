using LockGuard.Domain.Sessions;

namespace LockGuard.Domain.Deadlocks;

public interface IDeadlockVictimSelector
{
    string SelectVictim(DeadlockCycle cycle, IReadOnlyDictionary<string, SessionInfo> sessions, IReadOnlyDictionary<string, int> heldLockCount);
}

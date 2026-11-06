namespace LockGuard.Domain.Events;

public enum LockEventKind
{
    DeadlockDetected,
    DeadlockResolved,
    VictimForceReleased,
    OrphanedLockReclaimed,
}

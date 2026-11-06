namespace LockGuard.Domain.Coordination;

public enum AbortReason
{
    
    DeadlockVictim,

    LockReclaimed,
}

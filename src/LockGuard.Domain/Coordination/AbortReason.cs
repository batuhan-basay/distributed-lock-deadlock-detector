namespace LockGuard.Domain.Coordination;

public enum AbortReason
{
    /// <summary>The session was chosen as the victim of a wait-for cycle.</summary>
    DeadlockVictim,

    /// <summary>A lock of the session was reclaimed as stale / orphaned.</summary>
    LockReclaimed,
}

namespace LockGuard.Domain.Locks;

public enum AcquireStatus
{
    
    Contended = 0,

    Acquired = 1,

    AlreadyOwned = 2,
}

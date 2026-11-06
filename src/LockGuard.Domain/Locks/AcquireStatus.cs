namespace LockGuard.Domain.Locks;

/// <summary>Outcome of a single acquisition attempt against the lock store.</summary>
public enum AcquireStatus
{
    /// <summary>Another session owns the lock.</summary>
    Contended = 0,

    /// <summary>The lock was free and is now owned by the caller.</summary>
    Acquired = 1,

    /// <summary>The caller (same session) already owned the lock; TTL refreshed (idempotent re-acquisition).</summary>
    AlreadyOwned = 2,
}

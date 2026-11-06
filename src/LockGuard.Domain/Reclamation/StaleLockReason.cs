namespace LockGuard.Domain.Reclamation;

public enum StaleLockReason
{
    /// <summary>The owning instance has never sent a heartbeat (or its heartbeat entry was purged).</summary>
    OwnerInstanceUnknown,

    /// <summary>The owning instance stopped sending heartbeats: it crashed / was killed / is partitioned.</summary>
    OwnerInstanceDead,

    /// <summary>The key has no expiry; it would live forever (legacy client or manual SET).</summary>
    NoExpiry,

    /// <summary>The lock is held longer than the allowed maximum: zombie process whose watchdog keeps it alive.</summary>
    MaxHoldTimeExceeded,
}

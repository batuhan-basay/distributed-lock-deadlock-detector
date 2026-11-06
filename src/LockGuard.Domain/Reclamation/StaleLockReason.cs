namespace LockGuard.Domain.Reclamation;

public enum StaleLockReason
{
    
    OwnerInstanceUnknown,

    OwnerInstanceDead,

    NoExpiry,

    MaxHoldTimeExceeded,
}

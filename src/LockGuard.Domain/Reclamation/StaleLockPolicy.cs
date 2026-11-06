using LockGuard.Domain.Coordination;
using LockGuard.Domain.Locks;

namespace LockGuard.Domain.Reclamation;

public sealed record StaleLockPolicy(TimeSpan InstanceStaleAfter, TimeSpan MinLockAge, TimeSpan MaxHoldTime, bool ReclaimLocksWithoutExpiry)
{
    
    public StaleLockReason? Classify(LockInfo lockInfo, IReadOnlyDictionary<string, InstanceHeartbeat> instances, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(lockInfo);
        ArgumentNullException.ThrowIfNull(instances);

        if (InternalResources.IsInternal(lockInfo.Resource))
        {
            return null;
        }

        if (lockInfo.HeldFor(now) < MinLockAge)
        {
            return null; 
        }

        if (!instances.TryGetValue(lockInfo.InstanceId, out var heartbeat))
        {
            return StaleLockReason.OwnerInstanceUnknown;
        }

        if (now - heartbeat.LastSeenAt > InstanceStaleAfter)
        {
            return StaleLockReason.OwnerInstanceDead;
        }

        if (lockInfo.HasNoExpiry && ReclaimLocksWithoutExpiry)
        {
            return StaleLockReason.NoExpiry;
        }

        if (lockInfo.HeldFor(now) > MaxHoldTime)
        {
            return StaleLockReason.MaxHoldTimeExceeded;
        }

        return null;
    }

    public static string ToTag(StaleLockReason reason) => reason switch
    {
        StaleLockReason.OwnerInstanceUnknown => "owner_instance_unknown",
        StaleLockReason.OwnerInstanceDead => "owner_instance_dead",
        StaleLockReason.NoExpiry => "no_expiry",
        StaleLockReason.MaxHoldTimeExceeded => "max_hold_time_exceeded",
        _ => "unknown",
    };
}

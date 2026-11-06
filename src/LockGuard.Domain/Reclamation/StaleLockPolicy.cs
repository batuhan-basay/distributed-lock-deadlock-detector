using LockGuard.Domain.Coordination;
using LockGuard.Domain.Locks;

namespace LockGuard.Domain.Reclamation;

/// <summary>
/// Pure rule set deciding whether a lock is stale / orphaned. The reclaimer applies it; tests exercise it directly.
/// </summary>
/// <param name="InstanceStaleAfter">An instance whose heartbeat is older than this is considered crashed.</param>
/// <param name="MinLockAge">Locks younger than this are never reclaimed (protects against heartbeat/acquire races).</param>
/// <param name="MaxHoldTime">Upper bound for holding a lock (zombie protection).</param>
/// <param name="ReclaimLocksWithoutExpiry">Reclaim keys that were written without any expiry.</param>
public sealed record StaleLockPolicy(TimeSpan InstanceStaleAfter, TimeSpan MinLockAge, TimeSpan MaxHoldTime, bool ReclaimLocksWithoutExpiry)
{
    /// <summary>Returns the reason why <paramref name="lockInfo"/> is stale, or <c>null</c> when it is healthy.</summary>
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
            return null; // too young: the owner may not have published its first heartbeat yet
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

    /// <summary>Stable snake_case tag used in metrics and reports.</summary>
    public static string ToTag(StaleLockReason reason) => reason switch
    {
        StaleLockReason.OwnerInstanceUnknown => "owner_instance_unknown",
        StaleLockReason.OwnerInstanceDead => "owner_instance_dead",
        StaleLockReason.NoExpiry => "no_expiry",
        StaleLockReason.MaxHoldTimeExceeded => "max_hold_time_exceeded",
        _ => "unknown",
    };
}

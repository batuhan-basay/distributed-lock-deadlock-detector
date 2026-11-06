namespace LockGuard.Domain.Locks;

/// <summary>Snapshot of a lock currently stored in the lock store.</summary>
public sealed record LockInfo(
    string Resource,
    string SessionId,
    string ServiceName,
    string InstanceId,
    DateTimeOffset AcquiredAt,
    TimeSpan? RemainingTtl,
    long FencingToken)
{
    /// <summary><c>true</c> when the key has no expiry: it would live forever if its owner dies.</summary>
    public bool HasNoExpiry => RemainingTtl is null;

    public TimeSpan HeldFor(DateTimeOffset now) => now - AcquiredAt;
}

namespace LockGuard.Domain.Locks;

public sealed record LockInfo(
    string Resource,
    string SessionId,
    string ServiceName,
    string InstanceId,
    DateTimeOffset AcquiredAt,
    TimeSpan? RemainingTtl,
    long FencingToken)
{
    
    public bool HasNoExpiry => RemainingTtl is null;

    public TimeSpan HeldFor(DateTimeOffset now) => now - AcquiredAt;
}

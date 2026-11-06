namespace LockGuard.Domain.Locks;

public sealed record LockOwner(string SessionId, string ServiceName, string InstanceId);

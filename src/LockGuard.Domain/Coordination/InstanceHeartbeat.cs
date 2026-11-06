namespace LockGuard.Domain.Coordination;

public sealed record InstanceHeartbeat(string InstanceId, DateTimeOffset LastSeenAt);

namespace LockGuard.Domain.Coordination;

/// <summary>Instance liveness information (one entry per running process).</summary>
public sealed record InstanceHeartbeat(string InstanceId, DateTimeOffset LastSeenAt);

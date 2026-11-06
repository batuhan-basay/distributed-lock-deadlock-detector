namespace LockGuard.Application.Features.Locks.Queries.GetInstances;

public sealed record InstanceDto(string InstanceId, DateTimeOffset LastSeenAt, double SecondsSinceHeartbeat, bool Alive);

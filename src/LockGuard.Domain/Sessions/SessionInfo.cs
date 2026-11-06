namespace LockGuard.Domain.Sessions;

public sealed record SessionInfo(
    string SessionId,
    string ServiceName,
    string InstanceId,
    string? Name,
    int Priority,
    DateTimeOffset StartedAt);

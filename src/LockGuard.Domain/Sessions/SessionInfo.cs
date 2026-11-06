namespace LockGuard.Domain.Sessions;

/// <summary>Metadata of a running lock session, published to the coordination store.</summary>
public sealed record SessionInfo(
    string SessionId,
    string ServiceName,
    string InstanceId,
    string? Name,
    int Priority,
    DateTimeOffset StartedAt);

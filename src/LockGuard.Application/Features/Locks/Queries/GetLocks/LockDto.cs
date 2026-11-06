namespace LockGuard.Application.Features.Locks.Queries.GetLocks;

public sealed record LockDto(
    string Resource,
    string SessionId,
    string ServiceName,
    string InstanceId,
    long FencingToken,
    long HeldForMs,
    long? RemainingTtlMs,
    bool InternalLock);

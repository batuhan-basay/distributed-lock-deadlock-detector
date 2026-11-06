namespace LockGuard.Domain.Coordination;

public sealed record AbortSignal(string SessionId, AbortReason Reason, string Details, DateTimeOffset IssuedAt);

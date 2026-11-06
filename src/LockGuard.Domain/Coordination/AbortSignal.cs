namespace LockGuard.Domain.Coordination;

/// <summary>Signal written by the detector / reclaimer instructing a session to abort.</summary>
public sealed record AbortSignal(string SessionId, AbortReason Reason, string Details, DateTimeOffset IssuedAt);

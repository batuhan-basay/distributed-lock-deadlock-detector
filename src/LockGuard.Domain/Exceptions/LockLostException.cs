namespace LockGuard.Domain.Exceptions;

/// <summary>The session lost the lock (expired, or reclaimed as stale) and must stop working on the resource.</summary>
public sealed class LockLostException(string resource, string sessionId, string reason)
    : DistributedLockException($"Session '{sessionId}' lost lock on '{resource}': {reason}", resource, sessionId)
{
    public string Reason { get; } = reason;
}

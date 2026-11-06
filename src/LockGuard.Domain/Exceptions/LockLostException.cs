namespace LockGuard.Domain.Exceptions;

public sealed class LockLostException(string resource, string sessionId, string reason)
    : DistributedLockException($"Session '{sessionId}' lost lock on '{resource}': {reason}", resource, sessionId)
{
    public string Reason { get; } = reason;
}

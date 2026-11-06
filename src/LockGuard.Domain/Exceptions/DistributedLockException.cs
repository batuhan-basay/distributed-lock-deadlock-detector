namespace LockGuard.Domain.Exceptions;

/// <summary>Base type for lock failures that callers are expected to handle.</summary>
public abstract class DistributedLockException(string message, string resource, string sessionId, Exception? inner = null)
    : Exception(message, inner)
{
    public string Resource { get; } = resource;

    public string SessionId { get; } = sessionId;
}

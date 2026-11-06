using LockGuard.Core.Models;

namespace LockGuard.Core.Locking;

public abstract class DistributedLockException(string message, string resource, string sessionId, Exception? inner = null)
    : Exception(message, inner)
{
    public string Resource { get; } = resource;
    public string SessionId { get; } = sessionId;
}

/// <summary>The lock could not be acquired within the configured wait timeout.</summary>
public sealed class LockAcquisitionTimeoutException(string resource, string sessionId, string? holderSessionId, TimeSpan waited)
    : DistributedLockException(
        $"Session '{sessionId}' timed out after {waited.TotalMilliseconds:F0} ms waiting for '{resource}' (held by '{holderSessionId ?? "unknown"}').",
        resource, sessionId)
{
    public string? HolderSessionId { get; } = holderSessionId;
    public TimeSpan Waited { get; } = waited;
}

/// <summary>
/// The deadlock detector chose this session as the victim of a wait-for cycle. All locks of the session
/// must be released (disposing the session does that) and the unit of work retried.
/// </summary>
public sealed class DeadlockVictimException(string resource, string sessionId, AbortSignal signal)
    : DistributedLockException(
        $"Session '{sessionId}' was aborted as deadlock victim while waiting for '{resource}': {signal.Details}",
        resource, sessionId)
{
    public AbortSignal Signal { get; } = signal;
}

/// <summary>The session lost the lock (expired, or reclaimed as stale) and must stop working on the resource.</summary>
public sealed class LockLostException(string resource, string sessionId, string reason)
    : DistributedLockException($"Session '{sessionId}' lost lock on '{resource}': {reason}", resource, sessionId);

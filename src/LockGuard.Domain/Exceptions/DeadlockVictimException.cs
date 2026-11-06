using LockGuard.Domain.Coordination;

namespace LockGuard.Domain.Exceptions;

/// <summary>
/// The deadlock detector chose this session as the victim of a wait-for cycle. All locks of the session have been
/// released (victim rollback); the unit of work can be retried in a new session.
/// </summary>
public sealed class DeadlockVictimException(string resource, string sessionId, AbortSignal signal)
    : DistributedLockException(
        $"Session '{sessionId}' was aborted as deadlock victim while waiting for '{resource}': {signal.Details}",
        resource, sessionId)
{
    public AbortSignal Signal { get; } = signal;
}

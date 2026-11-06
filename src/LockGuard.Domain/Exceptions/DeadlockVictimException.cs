using LockGuard.Domain.Coordination;

namespace LockGuard.Domain.Exceptions;

public sealed class DeadlockVictimException(string resource, string sessionId, AbortSignal signal)
    : DistributedLockException(
        $"Session '{sessionId}' was aborted as deadlock victim while waiting for '{resource}': {signal.Details}",
        resource, sessionId)
{
    public AbortSignal Signal { get; } = signal;
}

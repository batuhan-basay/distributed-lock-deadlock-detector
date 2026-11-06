namespace LockGuard.Domain.Exceptions;

public sealed class LockAcquisitionTimeoutException(string resource, string sessionId, string? holderSessionId, TimeSpan waited)
    : DistributedLockException(
        $"Session '{sessionId}' timed out after {waited.TotalMilliseconds:F0} ms waiting for '{resource}' (held by '{holderSessionId ?? "unknown"}').",
        resource, sessionId)
{
    public string? HolderSessionId { get; } = holderSessionId;

    public TimeSpan Waited { get; } = waited;
}

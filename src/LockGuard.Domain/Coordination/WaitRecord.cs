namespace LockGuard.Domain.Coordination;

/// <summary>
/// "Session <see cref="WaiterSessionId"/> is blocked waiting for <see cref="Resource"/>".
/// The waiter refreshes <see cref="LastSeenAt"/> on every retry so abandoned records can be ignored.
/// </summary>
public sealed record WaitRecord(
    string WaiterSessionId,
    string Resource,
    string? ObservedHolderSessionId,
    string ServiceName,
    string InstanceId,
    DateTimeOffset WaitingSince,
    DateTimeOffset LastSeenAt);

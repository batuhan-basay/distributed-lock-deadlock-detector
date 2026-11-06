namespace LockGuard.Domain.Coordination;

public sealed record WaitRecord(
    string WaiterSessionId,
    string Resource,
    string? ObservedHolderSessionId,
    string ServiceName,
    string InstanceId,
    DateTimeOffset WaitingSince,
    DateTimeOffset LastSeenAt);

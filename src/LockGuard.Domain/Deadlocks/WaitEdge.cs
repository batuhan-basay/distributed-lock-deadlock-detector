namespace LockGuard.Domain.Deadlocks;

public sealed record WaitEdge(string WaiterSessionId, string HolderSessionId, string Resource, DateTimeOffset WaitingSince);

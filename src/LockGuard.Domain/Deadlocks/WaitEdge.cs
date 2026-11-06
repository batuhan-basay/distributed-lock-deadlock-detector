namespace LockGuard.Domain.Deadlocks;

/// <summary>Directed edge of the Wait-For Graph: <c>Waiter --(Resource)--&gt; Holder</c>.</summary>
public sealed record WaitEdge(string WaiterSessionId, string HolderSessionId, string Resource, DateTimeOffset WaitingSince);

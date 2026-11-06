namespace LockGuard.Domain.Locks;

/// <summary>
/// Identity of the logical owner of a lock. A <see cref="SessionId"/> represents a transaction / request scope:
/// every lock acquired by the same session is re-entrant for that session, regardless of which thread or process
/// performs the acquisition (idempotent retries reuse the same session id).
/// </summary>
/// <param name="SessionId">Logical owner (transaction, request, idempotency key).</param>
/// <param name="ServiceName">Name of the service that owns the session (e.g. <c>payment-api</c>).</param>
/// <param name="InstanceId">Process instance currently executing the session; used for crash detection.</param>
public sealed record LockOwner(string SessionId, string ServiceName, string InstanceId);

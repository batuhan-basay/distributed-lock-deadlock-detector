namespace LockGuard.Core.Models;

/// <summary>
/// Identity of the logical owner of a lock. A <see cref="SessionId"/> represents a transaction / request scope:
/// every lock acquired by the same session is re-entrant for that session, regardless of which
/// thread or process performs the acquisition (idempotent retries reuse the same session id).
/// </summary>
/// <param name="SessionId">Logical owner (transaction, request, idempotency key).</param>
/// <param name="ServiceName">Name of the service that owns the session (e.g. <c>payment-api</c>).</param>
/// <param name="InstanceId">Process instance currently executing the session; used for crash detection.</param>
public sealed record LockOwner(string SessionId, string ServiceName, string InstanceId);

/// <summary>Outcome of a single acquisition attempt against the lock store.</summary>
public enum AcquireStatus
{
    /// <summary>The lock was free and is now owned by the caller.</summary>
    Acquired = 1,

    /// <summary>The caller (same session) already owned the lock; TTL refreshed (idempotent re-acquisition).</summary>
    AlreadyOwned = 2,

    /// <summary>Another session owns the lock.</summary>
    Contended = 0,
}

/// <summary>Result of <see cref="Abstractions.ILockStore.TryAcquireAsync"/>.</summary>
public sealed record AcquireResult(
    AcquireStatus Status,
    long FencingToken,
    string? CurrentHolderSessionId,
    TimeSpan? Validity)
{
    public bool IsSuccess => Status is AcquireStatus.Acquired or AcquireStatus.AlreadyOwned;

    public static AcquireResult Contended(string? holder, TimeSpan? holderRemainingTtl) =>
        new(AcquireStatus.Contended, 0, holder, holderRemainingTtl);
}

/// <summary>Snapshot of a lock currently stored in the lock store.</summary>
public sealed record LockInfo(
    string Resource,
    string SessionId,
    string ServiceName,
    string InstanceId,
    DateTimeOffset AcquiredAt,
    TimeSpan? RemainingTtl,
    long FencingToken)
{
    /// <summary><c>true</c> when the key has no expiry: it would live forever if its owner dies.</summary>
    public bool HasNoExpiry => RemainingTtl is null;

    public TimeSpan HeldFor(DateTimeOffset now) => now - AcquiredAt;
}

/// <summary>Metadata of a running lock session, published to the coordination store.</summary>
public sealed record SessionInfo(
    string SessionId,
    string ServiceName,
    string InstanceId,
    string? Name,
    int Priority,
    DateTimeOffset StartedAt);

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

/// <summary>Instance liveness information (one entry per running process).</summary>
public sealed record InstanceHeartbeat(string InstanceId, DateTimeOffset LastSeenAt);

/// <summary>Signal written by the detector instructing a session to abort (deadlock victim or lock reclaimed).</summary>
public sealed record AbortSignal(string SessionId, AbortReason Reason, string Details, DateTimeOffset IssuedAt);

public enum AbortReason
{
    DeadlockVictim,
    LockReclaimed,
}

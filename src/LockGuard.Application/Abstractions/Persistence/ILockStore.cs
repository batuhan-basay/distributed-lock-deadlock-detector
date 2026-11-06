using LockGuard.Domain.Locks;

namespace LockGuard.Application.Abstractions.Persistence;

/// <summary>
/// Low-level, safety-critical lock storage (e.g. Redlock over N independent Redis nodes).
/// Every mutating operation is a compare-and-set on the owning session id, so a session can never
/// release or extend a lock that it does not own.
/// </summary>
public interface ILockStore
{
    /// <summary>
    /// Tries once to acquire <paramref name="resource"/> for <paramref name="owner"/> with the given TTL.
    /// If the session already owns the lock, the TTL is refreshed and <see cref="AcquireStatus.AlreadyOwned"/> is returned
    /// (idempotent acquisition: retries of the same request never block on themselves).
    /// </summary>
    Task<AcquireResult> TryAcquireAsync(string resource, LockOwner owner, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>Extends the TTL if (and only if) <paramref name="sessionId"/> still owns the lock.</summary>
    Task<bool> ExtendAsync(string resource, string sessionId, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the lock if (and only if) <paramref name="sessionId"/> owns it. Also used by the reclaimer /
    /// detector to force-release a lock of a dead or victim session (compare-and-delete: never steals a re-acquired lock).
    /// </summary>
    Task<bool> ReleaseAsync(string resource, string sessionId, CancellationToken cancellationToken = default);

    /// <summary>Returns the current holder of <paramref name="resource"/>, or <c>null</c> if free.</summary>
    Task<LockInfo?> GetAsync(string resource, CancellationToken cancellationToken = default);

    /// <summary>Enumerates every lock currently held (used by the detector and the reclaimer).</summary>
    Task<IReadOnlyList<LockInfo>> ListAsync(CancellationToken cancellationToken = default);
}

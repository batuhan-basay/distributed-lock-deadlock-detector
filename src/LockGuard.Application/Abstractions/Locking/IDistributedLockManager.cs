using LockGuard.Application.Locking;

namespace LockGuard.Application.Abstractions.Locking;

/// <summary>Entry point for application code that needs distributed locks.</summary>
/// <example>
/// <code>
/// await using var session = await locks.BeginSessionAsync(new() { Name = "transfer" }, ct);
/// await using var from = await session.AcquireAsync("account:A", ct);
/// await using var to   = await session.AcquireAsync("account:B", ct);
/// // ... critical section; pass session.LockLost to downstream I/O ...
/// </code>
/// </example>
public interface IDistributedLockManager
{
    /// <summary>
    /// Starts a lock session (the logical owner of a group of locks). Pass a stable <see cref="LockSessionOptions.SessionId"/>
    /// (e.g. an idempotency key) to make retried requests re-enter the locks they already hold.
    /// </summary>
    Task<LockSession> BeginSessionAsync(LockSessionOptions? options = null, CancellationToken cancellationToken = default);
}

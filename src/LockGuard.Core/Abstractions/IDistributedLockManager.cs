using LockGuard.Core.Locking;

namespace LockGuard.Core.Abstractions;

/// <summary>Entry point for application code.</summary>
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

public sealed class LockSessionOptions
{
    /// <summary>Explicit session id (idempotency key). A random id is generated when omitted.</summary>
    public string? SessionId { get; init; }

    /// <summary>Optional friendly name (shown in reports).</summary>
    public string? Name { get; init; }

    /// <summary>Lower priority sessions are preferred as deadlock victims.</summary>
    public int Priority { get; init; }

    public TimeSpan? LeaseTime { get; init; }

    public TimeSpan? WaitTimeout { get; init; }

    /// <summary>Disable watchdog extension for this session (the lock simply expires after <see cref="LeaseTime"/>).</summary>
    public bool DisableWatchdog { get; init; }
}

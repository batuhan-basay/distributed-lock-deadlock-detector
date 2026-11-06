namespace LockGuard.Application.Abstractions.Locking;

/// <summary>Per-session overrides for <see cref="IDistributedLockManager.BeginSessionAsync"/>.</summary>
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

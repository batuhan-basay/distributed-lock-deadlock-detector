namespace LockGuard.Application.Features.Simulations.Common;

/// <summary>Business outcome of a simulation; the API maps it to an HTTP status code.</summary>
public enum ScenarioOutcome
{
    /// <summary>The unit of work finished while holding all its locks (HTTP 200).</summary>
    Completed,

    /// <summary>The session was aborted by the deadlock detector; retryable (HTTP 409).</summary>
    DeadlockVictim,

    /// <summary>The lock could not be acquired within the wait timeout (HTTP 423).</summary>
    Timeout,

    /// <summary>A lock was lost (lease expired or reclaimed) before the work could commit (HTTP 409).</summary>
    LockLost,

    /// <summary>The requested resource is currently held by someone else (HTTP 409).</summary>
    ResourceBusy,

    /// <summary>The simulation was set up; its effect is observed asynchronously (HTTP 202).</summary>
    Accepted,
}

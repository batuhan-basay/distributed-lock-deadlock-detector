namespace LockGuard.Application.Abstractions.Locking;

public sealed class LockSessionOptions
{
    
    public string? SessionId { get; init; }

    public string? Name { get; init; }

    public int Priority { get; init; }

    public TimeSpan? LeaseTime { get; init; }

    public TimeSpan? WaitTimeout { get; init; }

    public bool DisableWatchdog { get; init; }
}

namespace LockGuard.Application.Features.Simulations.Common;

public enum ScenarioOutcome
{
    
    Completed,

    DeadlockVictim,

    Timeout,

    LockLost,

    ResourceBusy,

    Accepted,
}

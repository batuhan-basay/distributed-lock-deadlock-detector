using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.RunLongRunningJob;

/// <summary>
/// Holds a lock with a short lease (<see cref="LeaseMs"/>) for much longer work (<see cref="WorkMs"/>).
/// With the watchdog the lease keeps being extended; with <see cref="DisableWatchdog"/> the lease expires mid-work and the
/// ownership check before "commit" detects it.
/// </summary>
public sealed record RunLongRunningJobCommand(
    string Resource = "report:monthly",
    int WorkMs = 20_000,
    int LeaseMs = 3_000,
    bool DisableWatchdog = false) : IRequest<LongRunningJobResponse>;

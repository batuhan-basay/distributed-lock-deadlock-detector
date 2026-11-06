using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.RunLongRunningJob;

public sealed record RunLongRunningJobCommand(
    string Resource = "report:monthly",
    int WorkMs = 20_000,
    int LeaseMs = 3_000,
    bool DisableWatchdog = false) : IRequest<LongRunningJobResponse>;

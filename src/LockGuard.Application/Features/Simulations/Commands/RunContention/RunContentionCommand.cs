using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.RunContention;

public sealed record RunContentionCommand(
    string Resource = "inventory:sku-42",
    int WorkMs = 25,
    int WaitTimeoutMs = 10_000,
    string? SessionId = null) : IRequest<ContentionResponse>;

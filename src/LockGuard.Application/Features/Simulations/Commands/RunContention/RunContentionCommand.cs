using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.RunContention;

/// <summary>Acquires one (hot) resource, works for <see cref="WorkMs"/> and releases it. Fire many in parallel to create contention.</summary>
public sealed record RunContentionCommand(
    string Resource = "inventory:sku-42",
    int WorkMs = 25,
    int WaitTimeoutMs = 10_000,
    string? SessionId = null) : IRequest<ContentionResponse>;

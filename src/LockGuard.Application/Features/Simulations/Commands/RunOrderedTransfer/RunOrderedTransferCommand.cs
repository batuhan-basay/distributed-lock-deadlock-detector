using LockGuard.Application.Features.Simulations.Common;
using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.RunOrderedTransfer;

/// <summary>
/// Deadlock <em>prevention</em>: the caller lists the resources in any order, but they are acquired in a global
/// (ordinal) order, so two transfers can never form a wait-for cycle.
/// </summary>
public sealed record RunOrderedTransferCommand(
    string FirstResource,
    string SecondResource,
    int WorkMs = 200,
    string? SessionId = null) : IRequest<TransferResponse>;

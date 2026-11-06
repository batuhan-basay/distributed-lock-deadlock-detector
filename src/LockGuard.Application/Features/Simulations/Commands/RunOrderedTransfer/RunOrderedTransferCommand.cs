using LockGuard.Application.Features.Simulations.Common;
using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.RunOrderedTransfer;

public sealed record RunOrderedTransferCommand(
    string FirstResource,
    string SecondResource,
    int WorkMs = 200,
    string? SessionId = null) : IRequest<TransferResponse>;

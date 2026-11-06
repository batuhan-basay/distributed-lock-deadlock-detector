using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.SimulateCrashedOwner;

public sealed record SimulateCrashedOwnerCommand(string Resource = "payment:tx-1001", int TtlSeconds = 600)
    : IRequest<CrashedOwnerResponse>;

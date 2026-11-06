using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.RunThreeWayDeadlock;

public sealed record RunThreeWayDeadlockCommand(int HoldMs = 1000) : IRequest<ThreeWayDeadlockResponse>
{
    public static readonly IReadOnlyList<string> Resources = ["ledger:1", "ledger:2", "ledger:3"];
}

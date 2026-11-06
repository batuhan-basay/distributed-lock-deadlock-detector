using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.RunThreeWayDeadlock;

/// <summary>
/// Starts three sessions in parallel: <c>ledger:1 → ledger:2</c>, <c>ledger:2 → ledger:3</c>, <c>ledger:3 → ledger:1</c>.
/// The result is a three-party wait-for cycle that exactly one victim must break.
/// </summary>
public sealed record RunThreeWayDeadlockCommand(int HoldMs = 1000) : IRequest<ThreeWayDeadlockResponse>
{
    public static readonly IReadOnlyList<string> Resources = ["ledger:1", "ledger:2", "ledger:3"];
}

using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.RunReentrantScenario;

/// <summary>
/// Acquires the same resource <see cref="Depth"/> times in one session. Only the first acquisition reaches the store;
/// a retry carrying the same <see cref="SessionId"/> (idempotency key) is treated as <c>AlreadyOwned</c> instead of
/// blocking on its own lock.
/// </summary>
public sealed record RunReentrantScenarioCommand(
    string Resource = "order:42",
    int Depth = 3,
    int HoldMs = 0,
    string? SessionId = null) : IRequest<ReentrantScenarioResponse>;

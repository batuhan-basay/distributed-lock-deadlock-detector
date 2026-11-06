using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.RunReentrantScenario;

public sealed record RunReentrantScenarioCommand(
    string Resource = "order:42",
    int Depth = 3,
    int HoldMs = 0,
    string? SessionId = null) : IRequest<ReentrantScenarioResponse>;

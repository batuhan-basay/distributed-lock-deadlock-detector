using LockGuard.Application.Features.Simulations.Common;
using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.RunDeadlockScenario;

public sealed record RunDeadlockScenarioCommand(
    string Scenario,
    string FirstResource,
    string SecondResource,
    int HoldMs = 1500,
    int WorkMs = 200,
    int MaxRetries = 0,
    string? SessionId = null) : IRequest<TransferResponse>
{
    public const string ResourceA = "account:A";
    public const string ResourceB = "account:B";

    public static RunDeadlockScenarioCommand ScenarioA(int holdMs, int workMs, int maxRetries, string? sessionId) =>
        new("scenario-a", ResourceA, ResourceB, holdMs, workMs, maxRetries, sessionId);

    public static RunDeadlockScenarioCommand ScenarioB(int holdMs, int workMs, int maxRetries, string? sessionId) =>
        new("scenario-b", ResourceB, ResourceA, holdMs, workMs, maxRetries, sessionId);
}

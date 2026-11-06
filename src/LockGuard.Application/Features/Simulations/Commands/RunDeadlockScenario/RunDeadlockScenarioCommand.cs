using LockGuard.Application.Features.Simulations.Common;
using MediatR;

namespace LockGuard.Application.Features.Simulations.Commands.RunDeadlockScenario;

/// <summary>
/// Acquires <see cref="FirstResource"/>, waits <see cref="HoldMs"/>, then acquires <see cref="SecondResource"/>.
/// Two of these with opposite resource order, sent at the same time, deadlock on purpose; the detector resolves it.
/// </summary>
/// <param name="Scenario">Label used in responses and session names (<c>scenario-a</c> / <c>scenario-b</c>).</param>
/// <param name="MaxRetries">How many times a deadlock victim retries the whole unit of work in a new session.</param>
/// <param name="SessionId">Optional idempotency key for the first attempt.</param>
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

    /// <summary>account:A → account:B</summary>
    public static RunDeadlockScenarioCommand ScenarioA(int holdMs, int workMs, int maxRetries, string? sessionId) =>
        new("scenario-a", ResourceA, ResourceB, holdMs, workMs, maxRetries, sessionId);

    /// <summary>account:B → account:A (opposite order)</summary>
    public static RunDeadlockScenarioCommand ScenarioB(int holdMs, int workMs, int maxRetries, string? sessionId) =>
        new("scenario-b", ResourceB, ResourceA, holdMs, workMs, maxRetries, sessionId);
}

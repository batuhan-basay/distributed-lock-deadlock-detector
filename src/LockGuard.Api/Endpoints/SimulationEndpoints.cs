using LockGuard.Api.Http;
using LockGuard.Application.Features.Simulations.Commands.RunContention;
using LockGuard.Application.Features.Simulations.Commands.RunDeadlockScenario;
using LockGuard.Application.Features.Simulations.Commands.RunLongRunningJob;
using LockGuard.Application.Features.Simulations.Commands.RunOrderedTransfer;
using LockGuard.Application.Features.Simulations.Commands.RunReentrantScenario;
using LockGuard.Application.Features.Simulations.Commands.RunThreeWayDeadlock;
using LockGuard.Application.Features.Simulations.Commands.SimulateCrashedOwner;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LockGuard.Api.Endpoints;

public static class SimulationEndpoints
{
    public static IEndpointRouteBuilder MapSimulationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/test").WithTags("Simulation");

        group.MapPost("/deadlock-scenario-a", async (
                ISender sender,
                [FromHeader(Name = LockGuardHeaders.SessionId)] string? sessionId,
                int holdMs = 1500, int workMs = 200, int maxRetries = 0, CancellationToken ct = default) =>
            (await sender.Send(RunDeadlockScenarioCommand.ScenarioA(holdMs, workMs, maxRetries, sessionId), ct)).ToHttpResult());

        group.MapPost("/deadlock-scenario-b", async (
                ISender sender,
                [FromHeader(Name = LockGuardHeaders.SessionId)] string? sessionId,
                int holdMs = 1500, int workMs = 200, int maxRetries = 0, CancellationToken ct = default) =>
            (await sender.Send(RunDeadlockScenarioCommand.ScenarioB(holdMs, workMs, maxRetries, sessionId), ct)).ToHttpResult());

        group.MapPost("/deadlock-scenario-safe", async (
                ISender sender,
                [FromHeader(Name = LockGuardHeaders.SessionId)] string? sessionId,
                string first = RunDeadlockScenarioCommand.ResourceB, string second = RunDeadlockScenarioCommand.ResourceA,
                int workMs = 200, CancellationToken ct = default) =>
            (await sender.Send(new RunOrderedTransferCommand(first, second, workMs, sessionId), ct)).ToHttpResult());

        group.MapPost("/three-way-deadlock", async (ISender sender, int holdMs = 1000, CancellationToken ct = default) =>
            (await sender.Send(new RunThreeWayDeadlockCommand(holdMs), ct)).ToHttpResult());

        group.MapPost("/contention", async (
                ISender sender,
                [FromHeader(Name = LockGuardHeaders.SessionId)] string? sessionId,
                string resource = "inventory:sku-42", int workMs = 25, int waitTimeoutMs = 10_000, CancellationToken ct = default) =>
            (await sender.Send(new RunContentionCommand(resource, workMs, waitTimeoutMs, sessionId), ct)).ToHttpResult());

        group.MapPost("/long-running", async (
                ISender sender,
                string resource = "report:monthly", int workMs = 20_000, int leaseMs = 3_000, bool disableWatchdog = false,
                CancellationToken ct = default) =>
            (await sender.Send(new RunLongRunningJobCommand(resource, workMs, leaseMs, disableWatchdog), ct)).ToHttpResult());

        group.MapPost("/crash-simulation", async (
                ISender sender, string resource = "payment:tx-1001", int ttlSeconds = 600, CancellationToken ct = default) =>
            (await sender.Send(new SimulateCrashedOwnerCommand(resource, ttlSeconds), ct)).ToHttpResult());

        group.MapPost("/reentrant", async (
                ISender sender,
                [FromHeader(Name = LockGuardHeaders.SessionId)] string? sessionId,
                string resource = "order:42", int depth = 3, int holdMs = 0, CancellationToken ct = default) =>
            (await sender.Send(new RunReentrantScenarioCommand(resource, depth, holdMs, sessionId), ct)).ToHttpResult());

        return routes;
    }
}

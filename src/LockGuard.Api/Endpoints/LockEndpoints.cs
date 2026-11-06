using LockGuard.Application.Features.Locks.Queries.GetInstances;
using LockGuard.Application.Features.Locks.Queries.GetLockEvents;
using LockGuard.Application.Features.Locks.Queries.GetLocks;
using LockGuard.Application.Features.Locks.Queries.GetSessions;
using LockGuard.Application.Features.Locks.Queries.GetWaitForGraph;
using LockGuard.Domain.Events;
using MediatR;

namespace LockGuard.Api.Endpoints;

public static class LockEndpoints
{
    public static IEndpointRouteBuilder MapLockEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/locks").WithTags("Locks");

        group.MapGet("/", async (ISender sender, string? prefix = null, bool includeInternal = true, CancellationToken ct = default) =>
            Results.Ok(await sender.Send(new GetLocksQuery(prefix, includeInternal), ct)));

        group.MapGet("/wait-graph", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetWaitForGraphQuery(), ct)));

        group.MapGet("/events", async (ISender sender, int count = 50, LockEventKind? kind = null, CancellationToken ct = default) =>
            Results.Ok(await sender.Send(new GetLockEventsQuery(count, kind), ct)));

        group.MapGet("/sessions", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetSessionsQuery(), ct)));

        group.MapGet("/instances", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetInstancesQuery(), ct)));

        return routes;
    }
}

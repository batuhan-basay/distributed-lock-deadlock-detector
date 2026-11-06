using System.Text;
using LockGuard.Core.Abstractions;
using LockGuard.Core.Coordination;
using LockGuard.Core.Detection;
using LockGuard.Core.Models;

namespace LockGuard.Api.Endpoints;

/// <summary>Read-only views over the lock store and coordination data.</summary>
public static class DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/locks").WithTags("Diagnostics");

        group.MapGet("/", async (ILockStore store, TimeProvider time, CancellationToken ct) =>
        {
            var now = time.GetUtcNow();
            var locks = await store.ListAsync(ct);
            return Results.Ok(locks
                .OrderBy(l => l.Resource, StringComparer.Ordinal)
                .Select(l => new
                {
                    l.Resource,
                    l.SessionId,
                    l.ServiceName,
                    l.InstanceId,
                    l.FencingToken,
                    heldForMs = (long)l.HeldFor(now).TotalMilliseconds,
                    remainingTtlMs = l.RemainingTtl is { } ttl ? (long?)ttl.TotalMilliseconds : null,
                    internalLock = LockGuardInternal.IsInternal(l.Resource),
                }));
        });

        // Wait-For Graph + Mermaid rendering (paste into https://mermaid.live or a GitHub comment).
        group.MapGet("/wait-graph", async (DeadlockDetector detector, CancellationToken ct) =>
        {
            var snapshot = await detector.CaptureAsync(ct);
            return Results.Ok(new
            {
                snapshot.CapturedAt,
                edges = snapshot.Edges,
                cycles = snapshot.Cycles.Select(c => new { c.Sessions, description = c.Describe(), c.Edges }),
                deadlocked = snapshot.Cycles.Count > 0,
                mermaid = ToMermaid(snapshot),
            });
        });

        group.MapGet("/events", async (ICoordinationStore coordination, int count = 50, LockEventKind? kind = null, CancellationToken ct = default) =>
        {
            var events = await coordination.GetRecentEventsAsync(Math.Clamp(count, 1, 200), ct);
            return Results.Ok(kind is null ? events : events.Where(e => e.Kind == kind));
        });

        group.MapGet("/sessions", async (ICoordinationStore coordination, CancellationToken ct) =>
            Results.Ok((await coordination.GetSessionsAsync(ct)).Values.OrderBy(s => s.StartedAt)));

        group.MapGet("/instances", async (ICoordinationStore coordination, TimeProvider time, CancellationToken ct) =>
        {
            var now = time.GetUtcNow();
            return Results.Ok((await coordination.GetInstancesAsync(ct)).Values
                .Select(i => new { i.InstanceId, i.LastSeenAt, secondsSinceHeartbeat = (now - i.LastSeenAt).TotalSeconds }));
        });

        return routes;
    }

    internal static string ToMermaid(WaitForGraphSnapshot snapshot)
    {
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        string Id(string session)
        {
            if (!ids.TryGetValue(session, out var id))
            {
                ids[session] = id = $"n{ids.Count}";
            }

            return id;
        }

        var sb = new StringBuilder("graph LR\n");
        foreach (var edge in snapshot.Edges)
        {
            sb.Append($"  {Id(edge.WaiterSessionId)}[\"{edge.WaiterSessionId}\"] -- \"waits for {edge.Resource}\" --> ")
              .Append($"{Id(edge.HolderSessionId)}[\"{edge.HolderSessionId}\"]\n");
        }

        foreach (var session in snapshot.Cycles.SelectMany(c => c.Sessions).Distinct(StringComparer.Ordinal))
        {
            sb.Append($"  style {Id(session)} fill:#ffdddd,stroke:#cc0000\n");
        }

        return sb.ToString();
    }
}

using System.Diagnostics;
using LockGuard.Core;
using LockGuard.Core.Abstractions;
using LockGuard.Core.Locking;
using LockGuard.Core.Models;
using Microsoft.Extensions.Options;

namespace LockGuard.Api.Endpoints;

/// <summary>
/// Endpoints that reproduce lock problems on purpose so that the detector, reclaimer and watchdog can be watched live.
/// </summary>
public static class SimulationEndpoints
{
    public const string SessionHeader = "X-Lock-Session-Id";

    public static IEndpointRouteBuilder MapSimulationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/test").WithTags("Simulation");

        // --- Classic two-party deadlock --------------------------------------------------------------
        // Fire A and B at (almost) the same time: A holds account:A and waits for account:B, B holds account:B and waits
        // for account:A. The detector finds the cycle A -> B -> A, aborts one victim (HTTP 409) and the other completes.
        group.MapPost("/deadlock-scenario-a", (HttpContext ctx, IDistributedLockManager locks, int holdMs = 1500, int workMs = 200, int maxRetries = 0, CancellationToken ct = default) =>
            TransferAsync(ctx, locks, "account:A", "account:B", "scenario-a", holdMs, workMs, maxRetries, ordered: false, ct));

        group.MapPost("/deadlock-scenario-b", (HttpContext ctx, IDistributedLockManager locks, int holdMs = 1500, int workMs = 200, int maxRetries = 0, CancellationToken ct = default) =>
            TransferAsync(ctx, locks, "account:B", "account:A", "scenario-b", holdMs, workMs, maxRetries, ordered: false, ct));

        // --- Prevention: same opposite-order request, but locks are taken in global order ---------------
        group.MapPost("/deadlock-scenario-safe", (HttpContext ctx, IDistributedLockManager locks, string first = "account:B", string second = "account:A", int holdMs = 1500, int workMs = 200, CancellationToken ct = default) =>
            TransferAsync(ctx, locks, first, second, "scenario-safe", holdMs, workMs, maxRetries: 0, ordered: true, ct));

        // --- Three-party cycle, generated inside one process ----------------------------------------
        group.MapPost("/three-way-deadlock", ThreeWayDeadlockAsync);

        // --- High contention on a single hot key ---------------------------------------------------------
        group.MapPost("/contention", ContentionAsync);

        // --- Watchdog: work outlives the lease, the watchdog keeps extending it ---------------------------
        group.MapPost("/long-running", LongRunningAsync);

        // --- Orphaned lock: simulate a process that crashed while holding a lock ---------------------------
        group.MapPost("/crash-simulation", CrashSimulationAsync);

        // --- Re-entrancy + idempotent acquisition ----------------------------------------------------------
        group.MapPost("/reentrant", ReentrantAsync);

        return routes;
    }

    private static async Task<IResult> TransferAsync(
        HttpContext ctx, IDistributedLockManager locks, string first, string second, string scenario,
        int holdMs, int workMs, int maxRetries, bool ordered, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var attempts = new List<object>();

        for (var attempt = 0; ; attempt++)
        {
            await using var session = await locks.BeginSessionAsync(new LockSessionOptions
            {
                SessionId = attempt == 0 ? ctx.Request.Headers[SessionHeader].FirstOrDefault() : null,
                Name = $"{scenario}#{attempt + 1}",
            }, ct);

            try
            {
                var acquired = new List<LockHandle>();
                if (ordered)
                {
                    acquired.AddRange(await session.AcquireOrderedAsync([first, second], ct));
                }
                else
                {
                    acquired.Add(await session.AcquireAsync(first, ct));
                    await Task.Delay(holdMs, ct);              // widen the race window
                    acquired.Add(await session.AcquireAsync(second, ct));
                }

                await Task.Delay(workMs, session.LockLost); // "critical section" bound to lock ownership
                await session.EnsureLocksHeldAsync(ct);    // verify ownership right before "committing"

                return Results.Ok(new
                {
                    status = "completed",
                    scenario,
                    session = session.SessionId,
                    attempts = attempt + 1,
                    previousAttempts = attempts,
                    lockOrder = acquired.Select(h => new { h.Resource, h.FencingToken }),
                    elapsedMs = stopwatch.ElapsedMilliseconds,
                });
            }
            catch (DeadlockVictimException ex) when (attempt < maxRetries)
            {
                attempts.Add(new { session = session.SessionId, outcome = "deadlock_victim", ex.Signal.Details });
                // Back off longer than the lock retry delay so the surviving session can take the lock we just released.
                await Task.Delay(Random.Shared.Next(400, 900) * (attempt + 1), ct);
            }
            catch (DeadlockVictimException ex)
            {
                return Results.Json(new
                {
                    status = "deadlock_victim",
                    scenario,
                    session = session.SessionId,
                    retryable = true,
                    detail = ex.Signal.Details,
                    previousAttempts = attempts,
                    elapsedMs = stopwatch.ElapsedMilliseconds,
                }, statusCode: StatusCodes.Status409Conflict);
            }
            catch (LockAcquisitionTimeoutException ex)
            {
                return Results.Json(new
                {
                    status = "timeout",
                    scenario,
                    session = session.SessionId,
                    ex.Resource,
                    holder = ex.HolderSessionId,
                    waitedMs = ex.Waited.TotalMilliseconds,
                }, statusCode: StatusCodes.Status423Locked);
            }
            catch (Exception ex) when (ex is LockLostException || (ex is OperationCanceledException && session.LockLost.IsCancellationRequested))
            {
                return Results.Json(new
                {
                    status = "lock_lost",
                    scenario,
                    session = session.SessionId,
                    reason = session.LockLostReason ?? ex.Message,
                }, statusCode: StatusCodes.Status409Conflict);
            }
        }
    }

    private static async Task<IResult> ThreeWayDeadlockAsync(IDistributedLockManager locks, int holdMs = 1000, CancellationToken ct = default)
    {
        string[] resources = ["ledger:1", "ledger:2", "ledger:3"];
        var stopwatch = Stopwatch.StartNew();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 3).Select(async i =>
        {
            var mine = resources[i];
            var next = resources[(i + 1) % 3];
            await using var session = await locks.BeginSessionAsync(new LockSessionOptions { Name = $"three-way-{i + 1}" }, ct);
            try
            {
                await using var a = await session.AcquireAsync(mine, ct);
                await Task.Delay(holdMs, ct);
                await using var b = await session.AcquireAsync(next, ct);
                return new { worker = i + 1, session = session.SessionId, path = $"{mine} -> {next}", outcome = "completed", detail = (string?)null, elapsedMs = stopwatch.ElapsedMilliseconds };
            }
            catch (DeadlockVictimException ex)
            {
                return new { worker = i + 1, session = session.SessionId, path = $"{mine} -> {next}", outcome = "deadlock_victim", detail = (string?)ex.Signal.Details, elapsedMs = stopwatch.ElapsedMilliseconds };
            }
            catch (LockAcquisitionTimeoutException ex)
            {
                return new { worker = i + 1, session = session.SessionId, path = $"{mine} -> {next}", outcome = "timeout", detail = (string?)ex.Message, elapsedMs = stopwatch.ElapsedMilliseconds };
            }
        }));

        return Results.Ok(new
        {
            scenario = "three-way-deadlock",
            victims = outcomes.Count(o => o.outcome == "deadlock_victim"),
            completed = outcomes.Count(o => o.outcome == "completed"),
            outcomes,
        });
    }

    private static async Task<IResult> ContentionAsync(HttpContext ctx, IDistributedLockManager locks, string resource = "inventory:sku-42", int workMs = 25, int waitTimeoutMs = 10000, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        await using var session = await locks.BeginSessionAsync(new LockSessionOptions
        {
            SessionId = ctx.Request.Headers[SessionHeader].FirstOrDefault(),
            Name = "contention",
            WaitTimeout = TimeSpan.FromMilliseconds(waitTimeoutMs),
        }, ct);

        try
        {
            await using var handle = await session.AcquireAsync(resource, ct);
            var waited = stopwatch.ElapsedMilliseconds;
            await Task.Delay(workMs, session.LockLost);
            return Results.Ok(new { status = "completed", resource, session = session.SessionId, handle.FencingToken, waitedMs = waited, totalMs = stopwatch.ElapsedMilliseconds });
        }
        catch (LockAcquisitionTimeoutException ex)
        {
            return Results.Json(new { status = "timeout", resource, holder = ex.HolderSessionId, waitedMs = ex.Waited.TotalMilliseconds }, statusCode: StatusCodes.Status423Locked);
        }
    }

    private static async Task<IResult> LongRunningAsync(
        IDistributedLockManager locks, IOptions<LockGuardOptions> options,
        string resource = "report:monthly", int workMs = 20000, int leaseMs = 3000, bool disableWatchdog = false, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        await using var session = await locks.BeginSessionAsync(new LockSessionOptions
        {
            Name = "long-running",
            LeaseTime = TimeSpan.FromMilliseconds(leaseMs),
            DisableWatchdog = disableWatchdog,
        }, ct);

        await using var handle = await session.AcquireAsync(resource, ct);
        try
        {
            await Task.Delay(workMs, session.LockLost);
            await session.EnsureLocksHeldAsync(ct);
            return Results.Ok(new
            {
                status = "completed",
                resource,
                session = session.SessionId,
                leaseMs,
                workMs,
                watchdog = !disableWatchdog && options.Value.Watchdog.Enabled,
                expectedExtensions = disableWatchdog ? 0 : (int)(workMs / (leaseMs * options.Value.Watchdog.RenewalRatio)),
                elapsedMs = stopwatch.ElapsedMilliseconds,
            });
        }
        catch (Exception ex) when (ex is LockLostException || (ex is OperationCanceledException && session.LockLost.IsCancellationRequested))
        {
            return Results.Json(new
            {
                status = "lock_lost",
                resource,
                session = session.SessionId,
                reason = session.LockLostReason ?? ex.Message,
                hint = disableWatchdog ? "watchdog disabled: the lease expired while the work was still running" : null,
                elapsedMs = stopwatch.ElapsedMilliseconds,
            }, statusCode: StatusCodes.Status409Conflict);
        }
    }

    /// <summary>
    /// Writes a lock exactly like a real client would, but on behalf of an instance id that never sends a heartbeat
    /// (= a process that died right after acquiring). The lock has a long TTL, so without the reclaimer it would block the
    /// resource for <paramref name="ttlSeconds"/> seconds.
    /// </summary>
    private static async Task<IResult> CrashSimulationAsync(ILockStore store, IOptions<LockGuardOptions> options, string resource = "payment:tx-1001", int ttlSeconds = 600, CancellationToken ct = default)
    {
        var deadInstance = $"crashed-{Guid.NewGuid().ToString("N")[..8]}";
        var owner = new LockOwner($"s-orphan-{Guid.NewGuid():N}", options.Value.ServiceName, deadInstance);
        var result = await store.TryAcquireAsync(resource, owner, TimeSpan.FromSeconds(ttlSeconds), ct);
        if (!result.IsSuccess)
        {
            return Results.Json(new { status = "resource_busy", resource, holder = result.CurrentHolderSessionId }, statusCode: StatusCodes.Status409Conflict);
        }

        var reclaimer = options.Value.Reclaimer;
        return Results.Accepted("/api/locks/events", new
        {
            status = "orphaned_lock_created",
            resource,
            orphanSession = owner.SessionId,
            deadInstance,
            ttlSeconds,
            expectReclaimWithin = $"~{(reclaimer.MinLockAge + reclaimer.ScanInterval).TotalSeconds:F0}s (MinLockAge + ScanInterval), instead of {ttlSeconds}s",
        });
    }

    private static async Task<IResult> ReentrantAsync(HttpContext ctx, IDistributedLockManager locks, string resource = "order:42", int depth = 3, int holdMs = 0, CancellationToken ct = default)
    {
        await using var session = await locks.BeginSessionAsync(new LockSessionOptions
        {
            SessionId = ctx.Request.Headers[SessionHeader].FirstOrDefault(),
            Name = "reentrant",
        }, ct);

        var handles = new List<LockHandle>();
        try
        {
            for (var i = 0; i < Math.Clamp(depth, 1, 10); i++)
            {
                handles.Add(await session.AcquireAsync(resource, ct));
            }

            var heldCount = session.HeldResources.GetValueOrDefault(resource);
            if (holdMs > 0)
            {
                await Task.Delay(holdMs, ct);
            }

            return Results.Ok(new
            {
                status = "completed",
                resource,
                session = session.SessionId,
                holdCount = heldCount,
                acquisitions = handles.Select((h, i) => new { level = i + 1, h.IsReentrant, h.FencingToken }),
                note = "Only the first acquisition hit Redis; nested acquisitions incremented the in-session hold count. " +
                       $"Send the same '{SessionHeader}' header again (e.g. a client retry) and the store treats it as AlreadyOwned instead of blocking on itself.",
            });
        }
        finally
        {
            for (var i = handles.Count - 1; i >= 0; i--)
            {
                await handles[i].DisposeAsync();
            }
        }
    }
}

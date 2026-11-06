using LockGuard.Application.Abstractions.Locking;
using LockGuard.Application.Locking.Watchdog;
using LockGuard.Domain.Exceptions;
using LockGuard.UnitTests.TestDoubles;

namespace LockGuard.UnitTests.Application.Locking;

public class LockSessionTests
{
    [Fact]
    public async Task Reentrant_acquisition_increments_hold_count_and_releases_on_outermost_dispose()
    {
        await using var host = new LockGuardTestHost();
        await using var session = await host.Manager.BeginSessionAsync();

        var outer = await session.AcquireAsync("order:1");
        var middle = await session.AcquireAsync("order:1");
        var inner = await session.AcquireAsync("order:1");

        Assert.False(outer.IsReentrant);
        Assert.True(middle.IsReentrant);
        Assert.True(inner.IsReentrant);
        Assert.Equal(outer.FencingToken, inner.FencingToken);
        Assert.Equal(3, session.HeldResources["order:1"]);

        await inner.DisposeAsync();
        await middle.DisposeAsync();
        Assert.NotNull(await host.Store.GetAsync("order:1")); // still held by the outer handle

        await outer.DisposeAsync();
        Assert.Null(await host.Store.GetAsync("order:1"));
    }

    [Fact]
    public async Task Disposing_a_handle_twice_releases_only_once()
    {
        await using var host = new LockGuardTestHost();
        await using var session = await host.Manager.BeginSessionAsync();
        var outer = await session.AcquireAsync("r");
        var inner = await session.AcquireAsync("r");

        await inner.DisposeAsync();
        await inner.DisposeAsync();

        Assert.Equal(1, session.HeldResources["r"]);
        Assert.NotNull(await host.Store.GetAsync("r"));
        await outer.DisposeAsync();
    }

    [Fact]
    public async Task Same_session_id_on_another_instance_is_idempotent_and_does_not_block_itself()
    {
        // Two "processes" sharing the same stores. A client retries a request (same idempotency key) and the retry lands
        // on another instance while the original still holds the lock.
        await using var instance1 = new LockGuardTestHost(instanceId: "api-1");
        await using var instance2 = new LockGuardTestHost(sharedStore: instance1.Store, sharedCoordination: instance1.Coordination, instanceId: "api-2");

        await using var original = await instance1.Manager.BeginSessionAsync(new LockSessionOptions { SessionId = "idem-42" });
        var first = await original.AcquireAsync("payment:42");

        await using var retry = await instance2.Manager.BeginSessionAsync(new LockSessionOptions { SessionId = "idem-42", WaitTimeout = TimeSpan.FromMilliseconds(200) });
        var second = await retry.AcquireAsync("payment:42");

        Assert.True(second.IsReentrant);
        Assert.Equal(first.FencingToken, second.FencingToken);

        var info = await instance1.Store.GetAsync("payment:42");
        Assert.NotNull(info);
        Assert.Equal("api-2", info.InstanceId); // ownership adopted by the live instance
    }

    [Fact]
    public async Task Different_sessions_are_mutually_exclusive_under_parallel_load()
    {
        await using var host = new LockGuardTestHost();
        var counter = 0;
        const int workers = 40;
        const int incrementsPerWorker = 5;

        await Task.WhenAll(Enumerable.Range(0, workers).Select(async _ =>
        {
            for (var i = 0; i < incrementsPerWorker; i++)
            {
                await using var session = await host.Manager.BeginSessionAsync();
                await using var handle = await session.AcquireAsync("counter");

                // Deliberately non-atomic read-modify-write: only correct if the lock really excludes everyone else.
                var read = counter;
                await Task.Yield();
                counter = read + 1;
            }
        }));

        Assert.Equal(workers * incrementsPerWorker, counter);
    }

    [Fact]
    public async Task Fencing_tokens_increase_with_every_fresh_acquisition()
    {
        await using var host = new LockGuardTestHost();
        var tokens = new List<long>();
        for (var i = 0; i < 5; i++)
        {
            await using var session = await host.Manager.BeginSessionAsync();
            await using var handle = await session.AcquireAsync("fenced");
            tokens.Add(handle.FencingToken);
        }

        Assert.Equal(tokens.Order(), tokens);
        Assert.Equal(5, tokens.Distinct().Count());
    }

    [Fact]
    public async Task Contended_acquisition_times_out_and_cleans_its_wait_record()
    {
        await using var host = new LockGuardTestHost();
        await using var holder = await host.Manager.BeginSessionAsync();
        await using var held = await holder.AcquireAsync("busy");

        await using var waiter = await host.Manager.BeginSessionAsync(new LockSessionOptions { WaitTimeout = TimeSpan.FromMilliseconds(150) });
        var ex = await Assert.ThrowsAsync<LockAcquisitionTimeoutException>(() => waiter.AcquireAsync("busy"));

        Assert.Equal(holder.SessionId, ex.HolderSessionId);
        Assert.True(ex.Waited >= TimeSpan.FromMilliseconds(150));
        Assert.Empty(await host.Coordination.GetWaitsAsync());
    }

    [Fact]
    public async Task Waiting_session_publishes_its_edge_of_the_wait_for_graph()
    {
        await using var host = new LockGuardTestHost();
        await using var holder = await host.Manager.BeginSessionAsync();
        await using var held = await holder.AcquireAsync("busy");

        await using var waiter = await host.Manager.BeginSessionAsync(new LockSessionOptions { WaitTimeout = TimeSpan.FromSeconds(5) });
        var pending = waiter.AcquireAsync("busy");
        await Task.Delay(100);

        var snapshot = await host.Detector.CaptureAsync();
        var edge = Assert.Single(snapshot.Edges);
        Assert.Equal(waiter.SessionId, edge.WaiterSessionId);
        Assert.Equal(holder.SessionId, edge.HolderSessionId);
        Assert.Equal("busy", edge.Resource);

        await held.DisposeAsync();
        await using var acquired = await pending;
        Assert.False(acquired.IsReentrant);
    }

    [Fact]
    public async Task Ordered_acquisition_prevents_deadlock_without_any_detector()
    {
        await using var host = new LockGuardTestHost(configure: o => o.Acquisition.WaitTimeout = TimeSpan.FromSeconds(5));

        async Task Transfer(string from, string to)
        {
            await using var session = await host.Manager.BeginSessionAsync();
            var handles = await session.AcquireOrderedAsync([from, to]);
            await Task.Delay(50);
            foreach (var h in handles)
            {
                await h.DisposeAsync();
            }
        }

        // Opposite request orders, many times, concurrently. No detector is running: a deadlock would time out.
        await Task.WhenAll(Enumerable.Range(0, 10).SelectMany(_ => new[] { Transfer("account:A", "account:B"), Transfer("account:B", "account:A") }));
    }

    [Fact]
    public async Task Disposing_the_session_releases_everything_it_holds()
    {
        await using var host = new LockGuardTestHost();
        var session = await host.Manager.BeginSessionAsync();
        await session.AcquireAsync("a");
        await session.AcquireAsync("b");
        await session.AcquireAsync("b");

        await session.DisposeAsync();

        Assert.Empty(await host.Store.ListAsync());
        Assert.Empty(await host.Coordination.GetSessionsAsync());
    }
}

using LockGuard.Application.Abstractions.Locking;
using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Application.Features.Locks.Queries.GetInstances;
using LockGuard.Application.Features.Locks.Queries.GetLocks;
using LockGuard.Application.Features.Locks.Queries.GetSessions;
using LockGuard.Application.Features.Locks.Queries.GetWaitForGraph;
using LockGuard.Domain.Coordination;
using LockGuard.Domain.Locks;
using LockGuard.UnitTests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

namespace LockGuard.UnitTests.Application.Features.Locks;

public class LockQueryHandlerTests
{
    [Fact]
    public async Task Wait_for_graph_query_shows_the_waiting_edge_and_renders_mermaid()
    {
        await using var host = new MediatorTestHost();
        var locks = host.Services.GetRequiredService<IDistributedLockManager>();

        await using var holder = await locks.BeginSessionAsync();
        await using var held = await holder.AcquireAsync("account:A");
        await using var waiter = await locks.BeginSessionAsync(new LockSessionOptions { WaitTimeout = TimeSpan.FromSeconds(5) });
        var pending = waiter.AcquireAsync("account:A");
        await Task.Delay(100);

        var graph = await host.SendAsync(new GetWaitForGraphQuery());

        Assert.False(graph.Deadlocked);
        var edge = Assert.Single(graph.Edges);
        Assert.Equal(waiter.SessionId, edge.WaiterSessionId);
        Assert.Equal(holder.SessionId, edge.HolderSessionId);
        Assert.StartsWith("graph LR", graph.Mermaid);
        Assert.Contains("waits for account:A", graph.Mermaid);

        await held.DisposeAsync();
        await using var acquired = await pending;
    }

    [Fact]
    public async Task Get_locks_filters_by_prefix_and_internal_flag()
    {
        await using var host = new MediatorTestHost();
        var store = host.Services.GetRequiredService<ILockStore>();
        await store.TryAcquireAsync("order:1", new LockOwner("s1", "svc", "i"), TimeSpan.FromMinutes(1));
        await store.TryAcquireAsync("account:1", new LockOwner("s2", "svc", "i"), TimeSpan.FromMinutes(1));
        await store.TryAcquireAsync(InternalResources.Leader("x"), new LockOwner("leader", "svc", "i"), TimeSpan.FromMinutes(1));

        Assert.Equal(3, (await host.SendAsync(new GetLocksQuery())).Count);
        Assert.Equal(2, (await host.SendAsync(new GetLocksQuery(IncludeInternal: false))).Count);
        Assert.Equal("order:1", Assert.Single(await host.SendAsync(new GetLocksQuery("order:"))).Resource);
    }

    [Fact]
    public async Task Sessions_and_instances_queries_reflect_the_coordination_store()
    {
        await using var host = new MediatorTestHost();
        var coordination = host.Services.GetRequiredService<ICoordinationStore>();
        await coordination.HeartbeatAsync("api-1", DateTimeOffset.UtcNow);
        await coordination.HeartbeatAsync("api-2", DateTimeOffset.UtcNow.AddMinutes(-5));

        await using var session = await host.Services.GetRequiredService<IDistributedLockManager>()
            .BeginSessionAsync(new LockSessionOptions { Name = "report" });

        Assert.Equal("report", Assert.Single(await host.SendAsync(new GetSessionsQuery())).Name);
        var instances = await host.SendAsync(new GetInstancesQuery());
        Assert.True(instances.Single(i => i.InstanceId == "api-1").Alive);
        Assert.False(instances.Single(i => i.InstanceId == "api-2").Alive);
    }
}

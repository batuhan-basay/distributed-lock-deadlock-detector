using LockGuard.Core.Models;

namespace LockGuard.IntegrationTests;

/// <summary>Redlock quorum behaviour when nodes fail. Uses its own cluster because it pauses containers.</summary>
public class RedlockFaultToleranceTests(RedlockClusterFixture cluster) : IClassFixture<RedlockClusterFixture>
{
    [Fact]
    public async Task Lock_survives_one_failed_node_but_not_two()
    {
        await using var sp = cluster.CreateInstance($"ft-{Guid.NewGuid():N}:");
        var store = sp.Store;

        await cluster.Nodes[2].PauseAsync();
        try
        {
            // 2 of 3 nodes reachable -> quorum (2) reached.
            var withOneDown = await store.TryAcquireAsync("res:1", new LockOwner("s1", "svc", "i"), TimeSpan.FromSeconds(10));
            Assert.True(withOneDown.IsSuccess);
            Assert.True(await store.ExtendAsync("res:1", "s1", TimeSpan.FromSeconds(10)));

            await cluster.Nodes[1].PauseAsync();
            try
            {
                // 1 of 3 nodes reachable -> no quorum: acquisition must fail and be rolled back.
                var withTwoDown = await store.TryAcquireAsync("res:2", new LockOwner("s2", "svc", "i"), TimeSpan.FromSeconds(10));
                Assert.False(withTwoDown.IsSuccess);
                Assert.Null(withTwoDown.CurrentHolderSessionId);
                Assert.False(await store.ExtendAsync("res:1", "s1", TimeSpan.FromSeconds(10)));
            }
            finally
            {
                await cluster.Nodes[1].UnpauseAsync();
            }
        }
        finally
        {
            await cluster.Nodes[2].UnpauseAsync();
        }

        // The rolled-back attempt left nothing behind on the healthy node.
        Assert.Null(await store.GetAsync("res:2"));
    }
}

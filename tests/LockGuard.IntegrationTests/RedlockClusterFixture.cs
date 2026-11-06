using LockGuard.Core;
using LockGuard.Core.Abstractions;
using LockGuard.Core.Detection;
using LockGuard.Core.Hosting;
using LockGuard.Core.Reclaim;
using LockGuard.Redis;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Redis;

namespace LockGuard.IntegrationTests;

/// <summary>Three independent Redis masters (Redlock quorum = 2), started once per test class.</summary>
public sealed class RedlockClusterFixture : IAsyncLifetime
{
    public const string Image = "redis:7.4-alpine";

    public IReadOnlyList<RedisContainer> Nodes { get; } =
        [.. Enumerable.Range(0, 3).Select(_ => new RedisBuilder(Image).Build())];

    public string[] Endpoints => [.. Nodes.Select(n => n.GetConnectionString())];

    public Task InitializeAsync() => Task.WhenAll(Nodes.Select(n => n.StartAsync()));

    public Task DisposeAsync() => Task.WhenAll(Nodes.Select(n => n.DisposeAsync().AsTask()));

    /// <summary>
    /// Builds a fully wired LockGuard "instance" against the cluster. Hosted services are not started; tests drive the
    /// detector / reclaimer explicitly. Every call uses its own key prefix so tests never see each other's keys.
    /// </summary>
    public ServiceProvider CreateInstance(string keyPrefix, string instanceId = "it-instance", Action<LockGuardOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddLockGuard(o =>
            {
                o.ServiceName = "integration-tests";
                o.InstanceId = instanceId;
                o.Acquisition.InitialRetryDelay = TimeSpan.FromMilliseconds(5);
                o.Acquisition.MaxRetryDelay = TimeSpan.FromMilliseconds(50);
                o.Acquisition.WaitTimeout = TimeSpan.FromSeconds(15);
                o.Detection.ConfirmationScans = 2;
                o.Detection.ScanInterval = TimeSpan.FromMilliseconds(100);
                configure?.Invoke(o);
            })
            .UseRedlock(o =>
            {
                o.Endpoints = Endpoints;
                o.KeyPrefix = keyPrefix;
                o.NodeTimeout = TimeSpan.FromMilliseconds(300);
            });

        return services.BuildServiceProvider();
    }
}

internal static class ServiceProviderExtensions
{
    extension(IServiceProvider sp)
    {
        public IDistributedLockManager Locks => sp.GetRequiredService<IDistributedLockManager>();
        public ILockStore Store => sp.GetRequiredService<ILockStore>();
        public ICoordinationStore Coordination => sp.GetRequiredService<ICoordinationStore>();
        public DeadlockDetector Detector => sp.GetRequiredService<DeadlockDetector>();
        public OrphanedLockReclaimer Reclaimer => sp.GetRequiredService<OrphanedLockReclaimer>();
        public RedisNodePool Pool => sp.GetRequiredService<RedisNodePool>();
    }
}

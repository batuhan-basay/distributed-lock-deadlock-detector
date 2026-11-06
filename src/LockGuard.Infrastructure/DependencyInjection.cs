using LockGuard.Application.Abstractions.Persistence;
using LockGuard.Application.Common.Options;
using LockGuard.Infrastructure.BackgroundJobs;
using LockGuard.Infrastructure.Observability;
using LockGuard.Infrastructure.Persistence.InMemory;
using LockGuard.Infrastructure.Persistence.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LockGuard.Infrastructure;

/// <summary>Infrastructure layer registration (C# 14 extension members).</summary>
public static class DependencyInjection
{
    public const string StoreKey = "LockGuard:Store";

    extension(IServiceCollection services)
    {
        /// <summary>
        /// Binds <see cref="LockGuardOptions"/> from configuration, picks the persistence adapter
        /// (<c>LockGuard:Store</c> = <c>Redis</c> (default) | <c>InMemory</c>), registers the background jobs and OpenTelemetry.
        /// </summary>
        public IServiceCollection AddInfrastructure(IConfiguration configuration, string defaultServiceName)
        {
            services.AddOptions<LockGuardOptions>().Bind(configuration.GetSection(LockGuardOptions.SectionName));
            services.AddMetrics();

            var store = configuration[StoreKey] ?? "Redis";
            if (string.Equals(store, "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                services.AddInMemoryPersistence();
            }
            else
            {
                services.AddRedisPersistence(configuration);
            }

            services.AddLockGuardBackgroundJobs();
            services.AddLockGuardObservability(
                configuration["LockGuard:ServiceName"] ?? defaultServiceName,
                configuration["LockGuard:InstanceId"] ?? Environment.MachineName,
                configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

            return services;
        }

        /// <summary>Redlock over the independent Redis nodes configured under <c>LockGuard:Redis</c>.</summary>
        public IServiceCollection AddRedisPersistence(IConfiguration configuration)
        {
            services.AddOptions<RedisOptions>().Bind(configuration.GetSection(RedisOptions.SectionName));
            services.TryAddSingleton<RedisNodePool>();
            services.TryAddSingleton<ILockStore, RedlockLockStore>();
            services.TryAddSingleton<ICoordinationStore, RedisCoordinationStore>();
            return services;
        }

        /// <summary>Single-process stores (no Redis): local development and tests.</summary>
        public IServiceCollection AddInMemoryPersistence()
        {
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton(sp => new InMemoryLockStore(sp.GetRequiredService<TimeProvider>()));
            services.TryAddSingleton<ILockStore>(sp => sp.GetRequiredService<InMemoryLockStore>());
            services.TryAddSingleton(sp => new InMemoryCoordinationStore(sp.GetRequiredService<TimeProvider>()));
            services.TryAddSingleton<ICoordinationStore>(sp => sp.GetRequiredService<InMemoryCoordinationStore>());
            return services;
        }

        /// <summary>Heartbeat, watchdog loop and the leader-elected detector / reclaimer loops.</summary>
        public IServiceCollection AddLockGuardBackgroundJobs()
        {
            services.AddHostedService<InstanceHeartbeatService>();
            services.AddHostedService<LockWatchdogHostedService>();
            services.AddHostedService<DeadlockDetectorService>();
            services.AddHostedService<OrphanedLockReclaimerService>();
            return services;
        }
    }
}

using LockGuard.Core.Abstractions;
using LockGuard.Core.Detection;
using LockGuard.Core.Diagnostics;
using LockGuard.Core.InMemory;
using LockGuard.Core.Locking;
using LockGuard.Core.Reclaim;
using LockGuard.Core.Watchdog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LockGuard.Core.Hosting;

/// <summary>Fluent builder returned by <c>services.AddLockGuard(...)</c>; store packages add their own extensions to it.</summary>
public sealed class LockGuardBuilder(IServiceCollection services, IConfiguration? configuration)
{
    public IServiceCollection Services { get; } = services;

    public IConfiguration? Configuration { get; } = configuration;
}

/// <summary>DI registration, written with C# 14 extension members.</summary>
public static class LockGuardServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the lock manager, watchdog, instance heartbeat, deadlock detector and orphaned lock reclaimer.
        /// The detector / reclaimer loops only run where <c>LockGuard:Detection:Enabled</c> / <c>LockGuard:Reclaimer:Enabled</c> are true.
        /// </summary>
        public LockGuardBuilder AddLockGuard(IConfiguration configuration, Action<LockGuardOptions>? configure = null)
        {
            var optionsBuilder = services.AddOptions<LockGuardOptions>().Bind(configuration.GetSection(LockGuardOptions.SectionName));
            if (configure is not null)
            {
                optionsBuilder.PostConfigure(configure);
            }

            return services.AddLockGuardCore(configuration);
        }

        /// <summary>Configuration-less overload (tests, samples).</summary>
        public LockGuardBuilder AddLockGuard(Action<LockGuardOptions>? configure = null)
        {
            var optionsBuilder = services.AddOptions<LockGuardOptions>();
            if (configure is not null)
            {
                optionsBuilder.Configure(configure);
            }

            return services.AddLockGuardCore(null);
        }

        private LockGuardBuilder AddLockGuardCore(IConfiguration? configuration)
        {
            services.AddMetrics();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton(sp => new LockGuardTelemetry(sp.GetService<System.Diagnostics.Metrics.IMeterFactory>()));
            services.TryAddSingleton<IDeadlockVictimSelector, DefaultDeadlockVictimSelector>();

            services.TryAddSingleton<LockWatchdog>();
            services.TryAddSingleton<IDistributedLockManager, DistributedLockManager>();
            services.TryAddSingleton<DeadlockDetector>();
            services.TryAddSingleton<OrphanedLockReclaimer>();

            services.AddHostedService<InstanceHeartbeatService>();
            services.AddHostedService(sp => sp.GetRequiredService<LockWatchdog>());
            services.AddHostedService<DeadlockDetectorService>();
            services.AddHostedService<OrphanedLockReclaimerService>();

            return new LockGuardBuilder(services, configuration);
        }
    }

    extension(LockGuardBuilder builder)
    {
        /// <summary>Single-process stores (no Redis). Useful for local development and unit tests.</summary>
        public LockGuardBuilder UseInMemoryStores()
        {
            builder.Services.TryAddSingleton<InMemoryLockStore>(sp => new InMemoryLockStore(sp.GetRequiredService<TimeProvider>()));
            builder.Services.TryAddSingleton<ILockStore>(sp => sp.GetRequiredService<InMemoryLockStore>());
            builder.Services.TryAddSingleton<ICoordinationStore>(sp => new InMemoryCoordinationStore(sp.GetRequiredService<TimeProvider>()));
            return builder;
        }
    }
}

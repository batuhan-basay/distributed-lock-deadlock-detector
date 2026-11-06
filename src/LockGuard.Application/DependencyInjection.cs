using System.Diagnostics.Metrics;
using FluentValidation;
using LockGuard.Application.Abstractions.Locking;
using LockGuard.Application.Common.Behaviors;
using LockGuard.Application.Common.Options;
using LockGuard.Application.Common.Telemetry;
using LockGuard.Application.Detection;
using LockGuard.Application.Locking;
using LockGuard.Application.Locking.Watchdog;
using LockGuard.Application.Reclamation;
using LockGuard.Domain.Deadlocks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LockGuard.Application;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        
        public IServiceCollection AddApplication(Action<LockGuardOptions>? configure = null)
        {
            var assembly = typeof(DependencyInjection).Assembly;

            var options = services.AddOptions<LockGuardOptions>();
            if (configure is not null)
            {
                options.PostConfigure(configure);
            }

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(assembly);
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            });
            services.AddValidatorsFromAssembly(assembly);

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton(sp => new LockGuardTelemetry(sp.GetService<IMeterFactory>()));
            services.TryAddSingleton<IDeadlockVictimSelector, DefaultDeadlockVictimSelector>();
            services.TryAddSingleton<LockWatchdog>();
            services.TryAddSingleton<IDistributedLockManager, DistributedLockManager>();
            services.TryAddSingleton<DeadlockDetector>();   
            services.TryAddSingleton<OrphanedLockReclaimer>();

            return services;
        }
    }
}

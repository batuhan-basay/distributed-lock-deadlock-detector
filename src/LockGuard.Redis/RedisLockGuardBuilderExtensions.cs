using LockGuard.Core.Abstractions;
using LockGuard.Core.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;

namespace LockGuard.Redis;

public static class RedisLockGuardBuilderExtensions
{
    extension(LockGuardBuilder builder)
    {
        /// <summary>Uses Redlock over the configured independent Redis nodes (<c>LockGuard:Redis</c> section).</summary>
        public LockGuardBuilder UseRedlock(Action<RedisLockGuardOptions>? configure = null)
        {
            var options = builder.Services.AddOptions<RedisLockGuardOptions>();
            if (builder.Configuration is not null)
            {
                options.Bind(builder.Configuration.GetSection(RedisLockGuardOptions.SectionName));
            }

            if (configure is not null)
            {
                options.PostConfigure(configure);
            }

            builder.Services.TryAddSingleton<RedisNodePool>();
            builder.Services.TryAddSingleton<ILockStore, RedlockLockStore>();
            builder.Services.TryAddSingleton<ICoordinationStore, RedisCoordinationStore>();
            return builder;
        }
    }
}

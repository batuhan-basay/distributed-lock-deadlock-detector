using LockGuard.Core.Hosting;
using LockGuard.Redis;

namespace LockGuard.Hosting;

/// <summary>Chooses the lock store from configuration: <c>LockGuard:Store</c> = <c>Redis</c> (default) | <c>InMemory</c>.</summary>
public static class StoreSelectionExtensions
{
    extension(LockGuardBuilder builder)
    {
        public LockGuardBuilder UseConfiguredStore()
        {
            var store = builder.Configuration?["LockGuard:Store"] ?? "Redis";
            return string.Equals(store, "InMemory", StringComparison.OrdinalIgnoreCase)
                ? builder.UseInMemoryStores()
                : builder.UseRedlock();
        }
    }
}

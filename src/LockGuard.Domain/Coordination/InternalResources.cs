namespace LockGuard.Domain.Coordination;

public static class InternalResources
{
    public const string Prefix = "lockguard:";

    public static string Leader(string role) => $"{Prefix}leader:{role}";

    public static bool IsInternal(string resource) => resource.StartsWith(Prefix, StringComparison.Ordinal);
}

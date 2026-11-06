namespace LockGuard.Domain.Coordination;

/// <summary>Resources used by LockGuard itself (leader election). They are excluded from the WFG and from reclamation.</summary>
public static class InternalResources
{
    public const string Prefix = "lockguard:";

    public static string Leader(string role) => $"{Prefix}leader:{role}";

    public static bool IsInternal(string resource) => resource.StartsWith(Prefix, StringComparison.Ordinal);
}

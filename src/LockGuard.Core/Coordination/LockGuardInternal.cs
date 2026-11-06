namespace LockGuard.Core.Coordination;

/// <summary>Resources used by LockGuard itself (leader election). They are excluded from the WFG and from reclamation.</summary>
public static class LockGuardInternal
{
    public const string Prefix = "lockguard:";

    public static string LeaderResource(string role) => $"{Prefix}leader:{role}";

    public static bool IsInternal(string resource) => resource.StartsWith(Prefix, StringComparison.Ordinal);
}

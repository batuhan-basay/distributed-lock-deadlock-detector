namespace LockGuard.Domain.Locks;

/// <summary>Resource naming helpers.</summary>
public static class ResourceGroup
{
    /// <summary><c>order:42</c> → <c>order</c>. Used to keep metric label cardinality bounded.</summary>
    public static string Of(string resource)
    {
        var idx = resource.IndexOf(':', StringComparison.Ordinal);
        return idx > 0 ? resource[..idx] : resource;
    }
}

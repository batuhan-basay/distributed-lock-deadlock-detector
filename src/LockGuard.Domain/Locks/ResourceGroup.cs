namespace LockGuard.Domain.Locks;

public static class ResourceGroup
{
    
    public static string Of(string resource)
    {
        var idx = resource.IndexOf(':', StringComparison.Ordinal);
        return idx > 0 ? resource[..idx] : resource;
    }
}

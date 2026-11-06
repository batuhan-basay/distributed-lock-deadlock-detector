using System.Reflection;

namespace LockGuard.Infrastructure.Persistence.Redis;

/// <summary>Lua scripts shipped as embedded resources (see <c>Scripts/*.lua</c>).</summary>
internal static class LuaScripts
{
    public static readonly string Acquire = Load("acquire");
    public static readonly string Extend = Load("extend");
    public static readonly string Release = Load("release");
    public static readonly string Inspect = Load("inspect");

    private static string Load(string name)
    {
        var resource = $"LockGuard.Infrastructure.Persistence.Redis.Scripts.{name}.lua";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded Lua script '{resource}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

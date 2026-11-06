using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LockGuard.Infrastructure.Persistence.Redis;

/// <summary>One <see cref="ConnectionMultiplexer"/> per independent Redis master + the embedded Lua scripts.</summary>
public sealed partial class RedisNodePool : IDisposable
{
    private readonly ILogger<RedisNodePool> _logger;

    public RedisNodePool(IOptions<RedisOptions> options, ILogger<RedisNodePool> logger)
    {
        _logger = logger;
        Options = options.Value;
        if (Options.Endpoints.Length == 0)
        {
            throw new InvalidOperationException("LockGuard:Redis:Endpoints must contain at least one Redis endpoint.");
        }

        Nodes = [.. Options.Endpoints.Select(Connect)];
        Quorum = (Nodes.Count / 2) + 1;
        LogConnected(Nodes.Count, Quorum);
    }

    public RedisOptions Options { get; }

    public IReadOnlyList<IConnectionMultiplexer> Nodes { get; }

    /// <summary>N/2 + 1.</summary>
    public int Quorum { get; }

    public IDatabase CoordinationDatabase =>
        Nodes[Math.Clamp(Options.CoordinationNodeIndex, 0, Nodes.Count - 1)].GetDatabase();

    private IConnectionMultiplexer Connect(string endpoint)
    {
        var config = ConfigurationOptions.Parse(
            string.IsNullOrWhiteSpace(Options.ConnectionOptions) ? endpoint : $"{endpoint},{Options.ConnectionOptions}");

        // Keep retrying in the background: a node that is down must not prevent the app from starting
        // (Redlock tolerates a minority of failed nodes).
        config.AbortOnConnectFail = false;
        config.ConnectTimeout = 2000;
        config.SyncTimeout = 2000;
        config.AsyncTimeout = 2000;
        config.ClientName = "lockguard";
        return ConnectionMultiplexer.Connect(config);
    }

    public void Dispose()
    {
        foreach (var node in Nodes)
        {
            node.Dispose();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Redlock pool created with {Nodes} independent Redis node(s); quorum = {Quorum}")]
    private partial void LogConnected(int nodes, int quorum);
}

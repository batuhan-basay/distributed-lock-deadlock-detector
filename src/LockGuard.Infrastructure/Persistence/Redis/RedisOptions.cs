
namespace LockGuard.Infrastructure.Persistence.Redis;

/// <summary>Bound from <c>LockGuard:Redis</c>.</summary>
public sealed class RedisOptions
{
    public const string SectionName = "LockGuard:Redis";

    /// <summary>
    /// Independent Redis masters (NOT replicas of each other, NOT a cluster). With N endpoints a lock needs N/2+1 votes.
    /// Use an odd number: 3 tolerates one failed node, 5 tolerates two. A single endpoint degrades to a plain Redis lock.
    /// </summary>
    public string[] Endpoints { get; set; } = ["localhost:6379"];

    /// <summary>Index into <see cref="Endpoints"/> of the node that stores advisory coordination data (WFG, heartbeats, reports).</summary>
    public int CoordinationNodeIndex { get; set; }

    public string KeyPrefix { get; set; } = "lg:";

    /// <summary>Redlock clock drift factor: validity = ttl - elapsed - (ttl * factor + 2ms).</summary>
    public double ClockDriftFactor { get; set; } = 0.01;

    /// <summary>Per-node timeout for a single lock operation; must be much smaller than the lease time.</summary>
    public TimeSpan NodeTimeout { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Extra StackExchange.Redis options appended to every endpoint, e.g. <c>password=...,ssl=true</c>.</summary>
    public string? ConnectionOptions { get; set; }
}

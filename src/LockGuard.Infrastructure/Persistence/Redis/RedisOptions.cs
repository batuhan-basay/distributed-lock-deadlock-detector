
namespace LockGuard.Infrastructure.Persistence.Redis;

public sealed class RedisOptions
{
    public const string SectionName = "LockGuard:Redis";

    public string[] Endpoints { get; set; } = ["localhost:6379"];

    public int CoordinationNodeIndex { get; set; }

    public string KeyPrefix { get; set; } = "lg:";

    public double ClockDriftFactor { get; set; } = 0.01;

    public TimeSpan NodeTimeout { get; set; } = TimeSpan.FromMilliseconds(250);

    public string? ConnectionOptions { get; set; }
}

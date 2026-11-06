using LockGuard.Core;
using LockGuard.Core.Abstractions;
using LockGuard.Core.Hosting;
using LockGuard.Hosting;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddLockGuard(builder.Configuration, options =>
    {
        // This process exists to run the detector and the reclaimer.
        options.Detection.Enabled = true;
        options.Reclaimer.Enabled = true;
    })
    .UseConfiguredStore();

builder.AddLockGuardObservability(builder.Configuration["LockGuard:ServiceName"] ?? "lockguard-detector");

var app = builder.Build();

app.MapLockGuardOperationalEndpoints();

// Small status page: which background roles run here, and who is the leader of each.
app.MapGet("/", (IEnumerable<IHostedService> services, IOptions<LockGuardOptions> options) => Results.Ok(new
{
    service = options.Value.ServiceName,
    instance = options.Value.InstanceId,
    roles = services.OfType<LeaderElectedLoopService>().Select(s => new { role = s.GetType().Name, leader = s.IsLeader }),
}));

app.MapGet("/events", async (ICoordinationStore coordination, int count = 50, CancellationToken ct = default) =>
    Results.Ok(await coordination.GetRecentEventsAsync(Math.Clamp(count, 1, 200), ct)));

app.Run();

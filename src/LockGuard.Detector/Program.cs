using LockGuard.Application;
using LockGuard.Application.Common.Options;
using LockGuard.Application.Features.Locks.Queries.GetLockEvents;
using LockGuard.Infrastructure;
using LockGuard.Infrastructure.BackgroundJobs;
using LockGuard.Infrastructure.Observability;
using MediatR;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication(options =>
    {
        // This process exists to run the deadlock detector and the orphaned lock reclaimer.
        options.Detection.Enabled = true;
        options.Reclaimer.Enabled = true;
    })
    .AddInfrastructure(builder.Configuration, defaultServiceName: "lockguard-detector");

var app = builder.Build();

app.MapOperationalEndpoints();

// Small status page: which background roles run here, and whether this instance is the leader of each.
app.MapGet("/", (IEnumerable<IHostedService> services, IOptions<LockGuardOptions> options) => Results.Ok(new
{
    service = options.Value.ServiceName,
    instance = options.Value.InstanceId,
    roles = services.OfType<LeaderElectedLoopService>().Select(s => new { s.Role, leader = s.IsLeader }),
}));

app.MapGet("/events", async (ISender sender, int count = 50, CancellationToken ct = default) =>
    Results.Ok(await sender.Send(new GetLockEventsQuery(count), ct)));

app.Run();

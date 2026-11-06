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
        
        options.Detection.Enabled = true;
        options.Reclaimer.Enabled = true;
    })
    .AddInfrastructure(builder.Configuration, defaultServiceName: "lockguard-detector");

var app = builder.Build();

app.MapOperationalEndpoints();

app.MapGet("/", (IEnumerable<IHostedService> services, IOptions<LockGuardOptions> options) => Results.Ok(new
{
    service = options.Value.ServiceName,
    instance = options.Value.InstanceId,
    roles = services.OfType<LeaderElectedLoopService>().Select(s => new { s.Role, leader = s.IsLeader }),
}));

app.MapGet("/events", async (ISender sender, int count = 50, CancellationToken ct = default) =>
    Results.Ok(await sender.Send(new GetLockEventsQuery(count), ct)));

app.Run();

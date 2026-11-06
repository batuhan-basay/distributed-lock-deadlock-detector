using System.Text.Json.Serialization;
using LockGuard.Api.Endpoints;
using LockGuard.Api.ExceptionHandling;
using LockGuard.Application;
using LockGuard.Infrastructure;
using LockGuard.Infrastructure.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration, defaultServiceName: "lockguard-api");

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<DistributedLockExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapOperationalEndpoints();
app.MapSimulationEndpoints();
app.MapLockEndpoints();
app.MapGet("/", () => Results.Redirect("/api/locks/wait-graph")).ExcludeFromDescription();

app.Run();

/// <summary>Exposed for WebApplicationFactory-based tests.</summary>
public partial class Program;

using System.Text.Json.Serialization;
using LockGuard.Api.Endpoints;
using LockGuard.Core.Hosting;
using LockGuard.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddLockGuard(builder.Configuration)
    .UseConfiguredStore();

builder.AddLockGuardObservability(builder.Configuration["LockGuard:ServiceName"] ?? "lockguard-api");

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.MapLockGuardOperationalEndpoints();
app.MapSimulationEndpoints();
app.MapDiagnosticsEndpoints();
app.MapGet("/", () => Results.Redirect("/api/locks/wait-graph")).ExcludeFromDescription();

app.Run();

/// <summary>Exposed for WebApplicationFactory-based tests.</summary>
public partial class Program;

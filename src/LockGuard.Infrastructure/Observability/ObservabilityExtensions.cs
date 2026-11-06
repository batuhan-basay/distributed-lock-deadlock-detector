using LockGuard.Application.Common.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LockGuard.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    extension(IServiceCollection services)
    {
        
        public IServiceCollection AddLockGuardObservability(string serviceName, string instanceId, string? otlpEndpoint)
        {
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(serviceName, serviceInstanceId: instanceId))
                .WithMetrics(metrics => metrics
                    .AddMeter(LockGuardTelemetry.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddPrometheusExporter())
                .WithTracing(tracing =>
                {
                    tracing
                        .AddSource(LockGuardTelemetry.ActivitySourceName)
                        .AddAspNetCoreInstrumentation(o => o.Filter = ctx =>
                            !ctx.Request.Path.StartsWithSegments("/metrics") && !ctx.Request.Path.StartsWithSegments("/health"));

                    if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    {
                        tracing.AddOtlpExporter();
                    }
                });

            services.AddHealthChecks();
            return services;
        }
    }

    extension(IEndpointRouteBuilder endpoints)
    {
        
        public IEndpointRouteBuilder MapOperationalEndpoints()
        {
            endpoints.MapPrometheusScrapingEndpoint("/metrics");
            endpoints.MapHealthChecks("/health");
            return endpoints;
        }
    }
}

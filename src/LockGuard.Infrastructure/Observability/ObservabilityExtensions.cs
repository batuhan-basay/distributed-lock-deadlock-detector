using LockGuard.Application.Common.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LockGuard.Infrastructure.Observability;

/// <summary>OpenTelemetry wiring shared by the API and the Detector worker.</summary>
public static class ObservabilityExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Metrics: LockGuard meter + ASP.NET Core + runtime, exposed for Prometheus at <c>/metrics</c>.
        /// Traces: LockGuard activity source + ASP.NET Core, exported over OTLP when <paramref name="otlpEndpoint"/> is set.
        /// </summary>
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
        /// <summary>Maps <c>/metrics</c> (Prometheus) and <c>/health</c>.</summary>
        public IEndpointRouteBuilder MapOperationalEndpoints()
        {
            endpoints.MapPrometheusScrapingEndpoint("/metrics");
            endpoints.MapHealthChecks("/health");
            return endpoints;
        }
    }
}

using LockGuard.Core.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LockGuard.Hosting;

/// <summary>OpenTelemetry wiring shared by the API and the Detector worker (linked into both projects).</summary>
public static class ObservabilityExtensions
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// Metrics: LockGuard meter + ASP.NET Core + runtime, exposed for Prometheus at <c>/metrics</c>.
        /// Traces: LockGuard activity source + ASP.NET Core, exported over OTLP when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set.
        /// </summary>
        public WebApplicationBuilder AddLockGuardObservability(string serviceName)
        {
            var instanceId = builder.Configuration["LockGuard:InstanceId"] ?? Environment.MachineName;
            var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

            builder.Services.AddOpenTelemetry()
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

            builder.Services.AddHealthChecks();
            return builder;
        }
    }

    extension(WebApplication app)
    {
        public WebApplication MapLockGuardOperationalEndpoints()
        {
            app.MapPrometheusScrapingEndpoint("/metrics");
            app.MapHealthChecks("/health");
            return app;
        }
    }
}

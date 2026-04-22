using Common.Extensions;
using Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Orchestration;

public static class ServiceDefaultsExtensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        SharedExtensions.AddSharedContexts();

        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        var services = builder.Services;

        services.AddServiceDiscovery();

        services.ConfigureHttpClientDefaults(http => {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    extension(IHostApplicationBuilder builder)
    {
        private void ConfigureOpenTelemetry()
        {
            var serviceName = Environment.GetEnvironmentVariable("SERVICE_NAME") ??
                              builder.Environment.ApplicationName.ToLowerInvariant();
            builder.Logging.SetMinimumLevel(LogLevel.Trace);
            builder.Logging.AddProvider(new FileLoggerProvider(serviceName));

            builder.Logging.AddOpenTelemetry(logging => {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });

            builder.Services.AddOpenTelemetry()
                   .ConfigureResource(resourceBuilder => {
                       var serviceName = Environment.GetEnvironmentVariable("SERVICE_NAME");

                       if (serviceName == null)
                           return;

                       var instanceId = Environment.GetEnvironmentVariable("SERVICE_INSTANCE_ID") ??
                                        Environment.GetEnvironmentVariable("HOSTNAME") ?? serviceName;

                       resourceBuilder.AddService(serviceName,
                           autoGenerateServiceInstanceId: false,
                           serviceInstanceId: instanceId);
                   })
                   .WithMetrics(metrics => {
                       metrics.AddAspNetCoreInstrumentation()
                              .AddHttpClientInstrumentation()
                              .AddRuntimeInstrumentation()
                              .AddMeter("Microsoft.Orleans")
                              .AddMeter("Backend");
                   })
                   .WithTracing(tracing => {
                       foreach (var source in TraceExtensions.AllSources)
                           tracing.AddSource(source.Name);

                       tracing.AddSource(builder.Environment.ApplicationName)
                              .AddAspNetCoreInstrumentation(options => options.Filter = context =>
                                  !context.Request.Path.StartsWithSegments(HealthEndpointPath) &&
                                  !context.Request.Path.StartsWithSegments(AlivenessEndpointPath))
                              .AddHttpClientInstrumentation();

                       tracing.AddAspNetCoreInstrumentation()
                              .AddHttpClientInstrumentation();
                   })
                   .WithLogging();

            builder.AddOpenTelemetryExporters();
        }

        private void AddOpenTelemetryExporters()
        {
            var connection = builder.Configuration.GetConnectionString(ConnectionNames.OpenTelemetry);

            if (connection == null)
            {
                var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

                if (useOtlpExporter)
                    builder.Services.AddOpenTelemetry().UseOtlpExporter();

                return;
            }

            var uri = new Uri(connection);
            builder.Services.AddOpenTelemetry().UseOtlpExporter(OtlpExportProtocol.HttpProtobuf, uri);
        }

        private void AddDefaultHealthChecks()
        {
            builder.Services.AddHealthChecks()
                   .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"])
                   .AddCheck<OrleansReadyHealthCheck>("orleans", tags: ["ready"]);
        }
    }

    private const string ReadyEndpointPath = "/ready";

    public static void MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(HealthEndpointPath);

        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        app.MapHealthChecks(ReadyEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("ready")
        });
    }
}
using Common;
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

namespace Aspire;

public static class AspireExtensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        var services = builder.Services;

        services.AddServiceDiscovery();

        services.ConfigureHttpClientDefaults(http =>
            {
                http.AddStandardResilienceHandler();
                http.AddServiceDiscovery();
            }
        );

        return builder;
    }

    public static void MapDefaultEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment() == true)
        {
            app.MapHealthChecks(HealthEndpointPath);

            app.MapHealthChecks(
                AlivenessEndpointPath,
                new HealthCheckOptions
                {
                    Predicate = r => r.Tags.Contains("live")
                }
            );
        }
    }

    extension(IHostApplicationBuilder builder)
    {
        private void ConfigureOpenTelemetry()
        {
            builder.Logging.AddOpenTelemetry(logging =>
                {
                    logging.IncludeFormattedMessage = true;
                    logging.IncludeScopes = true;
                }
            );

            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resourceBuilder =>
                    {
                        var serviceName = Environment.GetEnvironmentVariable("SERVICE_NAME");

                        if (serviceName == null)
                            return;

                        resourceBuilder.AddService(serviceName);
                    }
                )
                .WithMetrics(metrics =>
                    {
                        metrics.AddAspNetCoreInstrumentation()
                            .AddHttpClientInstrumentation()
                            .AddRuntimeInstrumentation()
                            .AddMeter("Microsoft.Orleans")
                            .AddMeter("Backend");
                    }
                )
                .WithTracing(tracing =>
                    {
                        foreach (var source in TraceExtensions.AllSources)
                            tracing.AddSource(source.Name);

                        tracing.AddSource(builder.Environment.ApplicationName)
                            .AddAspNetCoreInstrumentation(options => options.Filter = context =>
                                context.Request.Path.StartsWithSegments(HealthEndpointPath) == false &&
                                context.Request.Path.StartsWithSegments(AlivenessEndpointPath) == false
                            )
                            .AddHttpClientInstrumentation();

                        tracing.AddAspNetCoreInstrumentation()
                            .AddHttpClientInstrumentation();
                    }
                )
                .WithLogging();

            builder.AddOpenTelemetryExporters();
        }

        private void AddOpenTelemetryExporters()
        {
            var connection = builder.Configuration.GetConnectionString(ConnectionNames.OpenTelemetry);

            if (connection == null)
            {
                var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

                if (useOtlpExporter == true)
                    builder.Services.AddOpenTelemetry().UseOtlpExporter();

                return;
            }

            var uri = new Uri(connection);
            builder.Services.AddOpenTelemetry().UseOtlpExporter(OtlpExportProtocol.HttpProtobuf, uri);
        }

        private void AddDefaultHealthChecks()
        {
            builder.Services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        }
    }
}
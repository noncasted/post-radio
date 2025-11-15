using Aspire;
using Audio;
using Console;
using Frontend;
using Infrastructure.Coordination;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using Infrastructure.Orleans;
using Infrastructure.TaskScheduling;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minio;
using MudBlazor.Services;
using ServiceLoop;
using Services;
using SoundCloudExplode;

namespace Common;

public static class ProjectsSetupExtensions
{
    public static IHostApplicationBuilder SetupCoordinator(this IHostApplicationBuilder builder)
    {
        // Basic services
        builder
            .AddServiceDefaults()
            .AddOrleansClient();

        // Cluster services
        builder
            .AddBase(ServiceTag.Coordinator);

        // Project services

        return builder;
    }

    public static IHostApplicationBuilder SetupSilo(this IHostApplicationBuilder builder)
    {
        // Basic services
        builder
            .AddServiceDefaults()
            .ConfigureSilo();

        // Cluster services
        builder
            .AddBase(ServiceTag.Silo);

        return builder;
    }

    public static IHostApplicationBuilder SetupConsole(this IHostApplicationBuilder builder)
    {
        // Basic services
        builder
            .AddServiceDefaults()
            .AddOrleansClient();

        // Cluster services
        builder
            .AddBase(ServiceTag.Console)
            .AddAudioServices()
            .AddMinIo()
            .AddCommonConsoleComponents();

        builder.Services
            .AddHttpClient()
            .ConfigureHttpClientDefaults(clientBuilder =>
            {
                clientBuilder.ConfigureHttpClient(client =>
                {
                    client.Timeout = TimeSpan.FromMinutes(10); // Увеличиваем таймаут для больших файлов
                });
            });

        builder.Services
            .AddMudServices()
            .AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddSingleton<SoundCloudClient>();

        return builder;
    }

    public static IHostApplicationBuilder SetupFrontend(this IHostApplicationBuilder builder)
    {
        // Basic services
        builder
            .AddServiceDefaults()
            .AddOrleansClient();

        // Cluster services
        builder
            .AddBase(ServiceTag.Frontend)
            .AddAudioServices()
            .AddMinIo();

        builder.Services.AddHttpClient();
        builder.Services.AddMudServices();
        builder.Services.AddSingleton<SoundCloudClient>();
        
        return builder;
    }

    private static IHostApplicationBuilder AddBase(this IHostApplicationBuilder builder, ServiceTag serviceTag)
    {
        if (builder is WebApplicationBuilder webBuilder)
            webBuilder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = true);

        builder.Services.AddHostedService<ClusterParticipantStartup>();

        builder
            .AddEnvironment(serviceTag)
            .AddStateAttributes()
            .AddServiceLoop()
            .AddMessaging()
            .AddOrleansUtils()
            .AddServiceDiscovery()
            .AddTaskScheduling()
            .AddClusterFeatures();

        builder.Services.Add<DbSource>()
            .As<IDbSource>();

        return builder;
    }

    private static IHostApplicationBuilder AddMinIo(this IHostApplicationBuilder builder)
    {
        var credentials = new MinioCredentials()
        {
            Endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT")!,
            AccessKey = Environment.GetEnvironmentVariable("MINIO_ACCESSKEY")!,
            SecretKey = Environment.GetEnvironmentVariable("MINIO_SECRETKEY")!
        };

        var clientBuilder = new MinioClient()
            .WithEndpoint(credentials.Endpoint)
            .WithCredentials(credentials.AccessKey, credentials.SecretKey);

        // Check if we need SSL based on endpoint
        if (!credentials.Endpoint.Contains(":") || credentials.Endpoint.Contains(":443"))
        {
            clientBuilder = clientBuilder.WithSSL();
        }

        var minioClient = clientBuilder.Build();

        builder.Services.AddSingleton(minioClient);

        builder.Services.Add<ObjectStorage>()
            .As<IObjectStorage>();

        return builder;
    }
}
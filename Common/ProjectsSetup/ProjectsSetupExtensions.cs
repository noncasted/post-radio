using Aspire;
using Audio;
using Console;
using Frontend;
using Images;
using Infrastructure.Coordination;
using Infrastructure.Discovery;
using Infrastructure.Loop;
using Infrastructure.Messaging;
using Infrastructure.Orleans;
using Infrastructure.TaskScheduling;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minio;
using MudBlazor.Services;
using SoundCloudExplode;

namespace Common;

public static class ProjectsSetupExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder SetupCoordinator()
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

        public IHostApplicationBuilder SetupSilo()
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

        public IHostApplicationBuilder SetupConsole()
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
                .AddSoundCloud()
                .AddBlazorComponents()
                .AddCommonConsoleComponents()
                .AddHttpClient()
                .AddImagesService();

            builder.Services.Add<OnlineListener>()
                .As<IOnlineListener>()
                .As<ICoordinatorSetupCompleted>();

            return builder;
        }

        public IHostApplicationBuilder SetupFrontend()
        {
            // Basic services
            builder
                .AddServiceDefaults()
                .AddOrleansClient();

            // Cluster services
            builder
                .AddBase(ServiceTag.Frontend)
                .AddAudioServices()
                .AddMinIo()
                .AddSoundCloud()
                .AddBlazorComponents()
                .AddHttpClient()
                .AddImagesService();

            builder.Services.Add<OnlineTracker>()
                .As<IOnlineTracker>()
                .As<ICoordinatorSetupCompleted>();

            return builder;
        }

        private IHostApplicationBuilder AddBase(ServiceTag serviceTag)
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

        private IHostApplicationBuilder AddMinIo()
        {
            var credentials = new MinioCredentials
            {
                Endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT")!,
                AccessKey = Environment.GetEnvironmentVariable("MINIO_ACCESSKEY")!,
                SecretKey = Environment.GetEnvironmentVariable("MINIO_SECRETKEY")!
            };

            var httpClientHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            var httpClient = new HttpClient(httpClientHandler);

            var clientBuilder = new MinioClient()
                .WithEndpoint(credentials.Endpoint)
                .WithCredentials(credentials.AccessKey, credentials.SecretKey)
                .WithHttpClient(httpClient);

            if (credentials.Endpoint.StartsWith("https://"))
                clientBuilder = clientBuilder.WithSSL();

            var minioClient = clientBuilder.Build();

            builder.Services.AddSingleton(minioClient);

            builder.Services.Add<ObjectStorage>()
                .As<IObjectStorage>();

            return builder;
        }

        private IHostApplicationBuilder AddSoundCloud()
        {
            var soundCloudHttpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            var soundCloudHttpClient = new HttpClient(soundCloudHttpHandler);
            var soundCloudClient = new SoundCloudClient(soundCloudHttpClient);
            builder.Services.AddSingleton(soundCloudClient);

            return builder;
        }

        private IHostApplicationBuilder AddBlazorComponents()
        {
            builder.Services
                .AddMudServices()
                .AddRazorComponents()
                .AddInteractiveServerComponents();

            return builder;
        }

        private IHostApplicationBuilder AddHttpClient()
        {
            builder.Services
                .AddHttpClient()
                .ConfigureHttpClientDefaults(clientBuilder =>
                    {
                        clientBuilder.ConfigureHttpClient(client => { client.Timeout = TimeSpan.FromMinutes(10); });

                        clientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                            {
                                ServerCertificateCustomValidationCallback = HttpClientHandler
                                    .DangerousAcceptAnyServerCertificateValidator
                            }
                        );
                    }
                );

            return builder;
        }

        private IHostApplicationBuilder AddImagesService()
        {
            builder.Services.Add<IImagesCollection, ImagesCollection>()
                .As<ICoordinatorSetupCompleted>();

            return builder;
        }
    }
}
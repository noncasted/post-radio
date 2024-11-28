using Extensions;
using Minio;
using Options;
using SoundCloudExplode;

namespace Core;

public static class EnvironmentExtensions
{
    public static void AddCredentials(this IHostApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment() == true)
            AddDev();
        else
            AddProd();

        return;

        void AddDev()
        {
            builder.Configuration.AddJsonFile("Settings/secrets.json");
            var options = builder.Configuration.GetSection("MinioCredentials").Get<MinioCredentials>()!;
            builder.Services.AddSingleton(options);
            
            var buildUrl = builder.Configuration.GetSection("BuildUrl").Get<BuildUrl>()!;
            builder.Services.AddSingleton(buildUrl);
        }

        void AddProd()
        {
            var minioCredentials = new MinioCredentials()
            {
                Endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT")!,
                AccessKey = Environment.GetEnvironmentVariable("MINIO_ACCESSKEY")!,
                SecretKey = Environment.GetEnvironmentVariable("MINIO_SECRETKEY")!
            };

            builder.Services.AddSingleton(minioCredentials);
            
            var buildUrl = new BuildUrl()
            {
                Value = Environment.GetEnvironmentVariable("BUILD_URL")!
            };
            
            builder.Services.AddSingleton(buildUrl);
        }
    }
    
    public static IHostApplicationBuilder AddDefaultServices(this IHostApplicationBuilder builder)
    {
        var credentials = builder.GetMinioCredentials();

        var minioClient = new MinioClient()
            .WithEndpoint(credentials.Endpoint)
            .WithCredentials(credentials.AccessKey, credentials.SecretKey)
            .Build();

        builder.Services.AddSingleton(typeof(MinioClient), minioClient);

        var soundCloud = new SoundCloudClient();
        builder.Services.AddSingleton(soundCloud);
        
        builder.AddOptionsFile("Settings/appsettings.minio.json");
        builder.AddOptions<MinioOptions>("MinioOptions");

        return builder;
    }

    private static MinioCredentials GetMinioCredentials(this IHostApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment() == true)
        {
            builder.Configuration.AddJsonFile("Settings/secrets.json");
            return builder.Configuration.GetSection("MinioCredentials").Get<MinioCredentials>()!;
        }

        return new MinioCredentials()
        {
            Endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT")!,
            AccessKey = Environment.GetEnvironmentVariable("MINIO_ACCESSKEY")!,
            SecretKey = Environment.GetEnvironmentVariable("MINIO_SECRETKEY")!
        };
    }
}
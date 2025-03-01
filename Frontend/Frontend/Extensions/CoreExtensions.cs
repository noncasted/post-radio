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

    public static void ConfigureCors(this IHostApplicationBuilder builder)
    {
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("cors", policy =>
            {
                var url = GetUrl();
                Console.WriteLine($"Configuring CORS for {url}");
                policy.WithOrigins(url)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });
        
        return;

        string GetUrl()
        {
            if (builder.Environment.IsDevelopment() == true)
                return "https://post-radio.io";

            return Environment.GetEnvironmentVariable("BUILD_URL")!;
        }
    }
}
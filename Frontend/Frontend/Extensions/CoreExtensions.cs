using Minio;
using SoundCloudExplode;

namespace Frontend.Extensions;

public static class EnvironmentExtensions
{
    public static void AddCredentials(this IHostApplicationBuilder builder)
    {
        var minioCredentials = new MinioCredentials()
        {
            Endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT")!,
            AccessKey = Environment.GetEnvironmentVariable("MINIO_ACCESSKEY")!,
            SecretKey = Environment.GetEnvironmentVariable("MINIO_SECRETKEY")!
        };

        builder.Services.AddSingleton(minioCredentials);
    }

    public static IHostApplicationBuilder AddDefaultServices(this IHostApplicationBuilder builder)
    {
        var credentials = new MinioCredentials()
        {
            Endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT")!,
            AccessKey = Environment.GetEnvironmentVariable("MINIO_ACCESSKEY")!,
            SecretKey = Environment.GetEnvironmentVariable("MINIO_SECRETKEY")!
        };

        var minioClient = new MinioClient()
            .WithEndpoint(credentials.Endpoint)
            .WithCredentials(credentials.AccessKey, credentials.SecretKey)
            .Build();

        builder.Services.AddSingleton(typeof(MinioClient), minioClient);

        var soundCloud = new SoundCloudClient();
        builder.Services.AddSingleton(soundCloud);

        return builder;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minio;

namespace Common;

public static class ObjectStorageServicesExtensions
{
    public static IHostApplicationBuilder AddObjectStorage(this IHostApplicationBuilder builder)
    {
        var credentials = builder.Configuration.GetSection("Minio").Get<MinioCredentials>();

        if (credentials == null)
            return builder;

        builder.Services.AddSingleton(credentials);

        builder.Services.AddSingleton<IMinioClient>(_ => {
            var client = new MinioClient()
                         .WithEndpoint(credentials.Endpoint)
                         .WithCredentials(credentials.AccessKey, credentials.SecretKey);

            if (credentials.UseSsl)
                client = client.WithSSL();

            return client.Build();
        });

        builder.Services.AddSingleton<IObjectStorage, ObjectStorage>();

        return builder;
    }
}

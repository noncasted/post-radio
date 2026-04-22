using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Common;

public static class MediaStorageServicesExtensions
{
    public static IHostApplicationBuilder AddMediaStorage(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<MediaStorageOptions>(builder.Configuration.GetSection("MediaStorage"));
        builder.Services.AddSingleton<IMediaStorage, MediaStorage>();
        return builder;
    }
}

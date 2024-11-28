using Images.API;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Images;

public static class ImageServicesExtensions
{
    public static IHostApplicationBuilder AddImageServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IImageRepository, ImageRepository>();
        builder.Services.AddSingleton<IImageAPI, ImageAPI>();
        return builder;
    }
}
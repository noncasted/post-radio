using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Common;

public static class CorsExtensions
{
    public static void ConfigureCors(this IHostApplicationBuilder builder)
    {
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("cors", policy =>
            {
               // var url = GetUrl();

                policy
                    .AllowAnyOrigin()
                    // .SetIsOriginAllowed(origin =>
                    // {
                    //     var uri = new Uri(origin);
                    //     return uri.Host == "localhost" || uri.Host == "127.0.0.1";
                    // })
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        return;

        // string GetUrl()
        // {
        //     return "*";
        //
        //     if (builder.Environment.IsDevelopment() == true)
        //         return "*";
        //
        //     return Environment.GetEnvironmentVariable("BUILD_URL")!;
        // }
    }
}
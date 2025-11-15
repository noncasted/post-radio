using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Common;

public static class OptionsExtensions
{
    public static void AddEnvironmentOptions<T>(this IHostApplicationBuilder builder, string filePath) where T : class
    {
        var rootPath = AppDomain.CurrentDomain.BaseDirectory;
        var path = Path.Combine(rootPath, GetFileName());
        builder.Configuration.AddJsonFile(path, optional: false);

        var section = builder.Configuration.GetSection(typeof(T).Name);
        builder.Services.Configure<T>(section);

        string GetFileName()
        {
            if (builder.Environment.IsDevelopment())
                return $"{filePath}.Development.json";

            return $"{filePath}.json";
        }
    }
}
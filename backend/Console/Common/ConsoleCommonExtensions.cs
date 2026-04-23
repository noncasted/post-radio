using Common.Extensions;
using Console.Actions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Console;

public static class ConsoleCommonExtensions
{
    public static IHostApplicationBuilder AddCommonConsoleComponents(this IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IConsoleNavigation, ConsoleNavigation>();
        builder.Add<InvalidTracksRedownloadConsoleAction>()
               .As<IConsoleAction>();
        builder.Add<TrackDurationRepairConsoleAction>()
               .As<IConsoleAction>();

        return builder;
    }
}
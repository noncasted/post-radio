using Common;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.TaskScheduling;

public static class TaskSchedulingExtensions
{
    public static IHostApplicationBuilder AddTaskScheduling(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.Add<TaskScheduler>()
            .As<ITaskScheduler>();

        services.Add<TaskQueue>()
            .As<ITaskQueue>();

        services.Add<TaskBalancer>()
            .As<ITaskBalancer>();

        return builder;
    }
}
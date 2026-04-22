using Common.Extensions;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Execution;

public static class TaskSchedulingExtensions
{
    public static IHostApplicationBuilder AddTaskScheduling(this IHostApplicationBuilder builder)
    {
        builder.Add<TaskScheduler>()
               .As<ITaskScheduler>();

        builder.Add<TaskQueue>()
               .As<ITaskQueue>();

        builder.Add<TaskBalancer>()
               .As<ITaskBalancer>();

        return builder;
    }
}
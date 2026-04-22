using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public interface ILocalSetupCompleted
{
    Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime);
}

public interface IOrleansStarted
{
    Task OnOrleansStarted(IReadOnlyLifetime lifetime);
}

public interface ICoordinatorSetupCompleted
{
    Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime);
}

public interface IServiceStarted
{
    Task OnServiceStarted(IReadOnlyLifetime lifetime);
}

public static class LoopExtensions
{
    public static IHostApplicationBuilder AddServiceLoop(this IHostApplicationBuilder builder)
    {
        builder.Add<ServiceLoopObserver>()
               .As<IServiceLoopObserver>()
               .As<ILifecycleParticipant<IClusterClientLifecycle>>()
               .As<ILifecycleParticipant<ISiloLifecycle>>();

        builder.Add<ServiceLoop>()
               .As<IServiceLoop>();

        return builder;
    }

    public static ContainerExtensions.Registration AsSetupLoopStage(this ContainerExtensions.Registration registration)
    {
        return registration
            .As<ILocalSetupCompleted>();
    }
}
using Common;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Loop;

public interface ILocalSetupCompleted
{
    Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime);
}

public interface ICoordinatorSetupCompleted
{
    Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime);
}

public static class LoopExtensions
{
    public static IHostApplicationBuilder AddServiceLoop(this IHostApplicationBuilder builder)
    {
        builder.Services.Add<ServiceLoopObserver>()
            .As<IServiceLoopObserver>()
            .As<ILifecycleParticipant<IClusterClientLifecycle>>()
            .As<ILifecycleParticipant<ISiloLifecycle>>();
        
        builder.Services.Add<ServiceLoop>()
            .As<IServiceLoop>();
        
        return builder;
    }

    public static ContainerExtensions.Registration AsSetupLoopStage(
        this ContainerExtensions.Registration registration)
    {
        return registration
            .As<ILocalSetupCompleted>();
    }
}
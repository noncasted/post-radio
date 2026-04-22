using Common.Extensions;
using Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public static class StateCollectionExtensions
{
    public static ContainerExtensions.Registration AddStateCollection<T, TKey, TState>(
        this IHostApplicationBuilder builder)
        where T : StateCollection<TKey, TState>
        where TKey : notnull
        where TState : class, IStateValue, new()
    {
        builder.Services.AddSingleton(typeof(StateCollectionUtils<,>));
        builder.Services.AddSingleton(typeof(IStateCollection<,>), typeof(StateCollection<,>));

        return builder.Add<T>()
                      .As<ILocalSetupCompleted>();

    }
}
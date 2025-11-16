using Common;
using Infrastructure.Loop;
using Infrastructure.Messaging;
using Infrastructure.Orleans;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.StorableActions;

public interface IClusterState<T> : IViewableProperty<T> where T : class, new()
{
    Task SetValue(T value);
}

public class ClusterState<T> : ViewableProperty<T>, ILocalSetupCompleted, IClusterState<T> where T : class, new()
{
    public ClusterState(IOrleans orleans, IMessaging messaging) : base(new T())
    {
        _orleans = orleans;
        _messaging = messaging;
    }

    private readonly IMessaging _messaging;

    private readonly IOrleans _orleans;

    public Task SetValue(T value)
    {
        Set(value);
        return _orleans.SetClusterState(value);
    }

    public async Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        _messaging.ListenQueue<T>(lifetime, new ClusterStateMessageQueueId<T>(), OnUpdate);

        var currentValue = await _orleans.GetClusterState<T>();
        Set(currentValue);

        OnSetup(lifetime);
    }

    private void OnUpdate(T value)
    {
        Set(value);
    }

    protected virtual void OnSetup(IReadOnlyLifetime lifetime)
    {
    }
}

public static class ClusterStateExtensions
{
    public static IHostApplicationBuilder AddClusterState<T>(this IHostApplicationBuilder builder)
        where T : class, new()
    {
        builder.Services.Add<ClusterState<T>>()
            .As<IClusterState<T>>()
            .As<ILocalSetupCompleted>();

        return builder;
    }
}
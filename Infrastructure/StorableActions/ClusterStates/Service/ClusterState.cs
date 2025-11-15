using Common;
using Infrastructure.Messaging;
using Infrastructure.Orleans;
using Interfaces;
using Microsoft.Extensions.Hosting;
using ServiceLoop;

namespace Service;

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

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;

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

    public Task SetValue(T value)
    {
        Set(value);
        return _orleans.SetClusterState(value);
    }

    protected virtual void OnSetup(IReadOnlyLifetime lifetime){}
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
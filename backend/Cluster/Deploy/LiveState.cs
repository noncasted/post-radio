using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cluster.Deploy;

public interface ILiveState<T> : IViewableProperty<T> where T : class, new()
{
    Task SetValue(T value);
}

public class LiveStateChannelId<T> : IRuntimeChannelId
{
    public LiveStateChannelId(Guid deployId)
    {
        _deployId = deployId;
    }

    private readonly Guid _deployId;

    public string ToRaw()
    {
        var type = typeof(T);
        return $"live-state-{type.FullName}-{_deployId:N}";
    }
}

public class LiveState<T> : ViewableProperty<T>, ILiveState<T>, IDeployAware where T : class, new()
{
    public LiveState(
        IMessaging messaging,
        ILogger<LiveState<T>> logger,
        T baseValue) : base(baseValue)
    {
        _messaging = messaging;
        _logger = logger;
    }

    private readonly IMessaging _messaging;
    private readonly ILogger<LiveState<T>> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private Guid _deployId;

    public async Task OnDeployChanged(Guid newDeployId, IReadOnlyLifetime deployLifetime)
    {
        _deployId = newDeployId;

        Set(new T());

        try
        {
            await _messaging.ListenChannel<T>(deployLifetime, new LiveStateChannelId<T>(newDeployId), OnUpdate);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[LiveState] Failed to listen channel for {Type} in deploy {DeployId}",
                typeof(T).Name, newDeployId);
        }
    }

    public async Task SetValue(T value)
    {
        await _writeLock.WaitAsync();

        try
        {
            Set(value);

            if (_deployId == Guid.Empty)
                return;

            await _messaging.PublishChannel(new LiveStateChannelId<T>(_deployId), value);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[LiveState] Failed to publish {Type}: {Value}", typeof(T).Name, value);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void OnUpdate(T value)
    {
        Set(value);
    }
}

public static class LiveStateExtensions
{
    public static IHostApplicationBuilder AddLiveState<T>(this IHostApplicationBuilder builder)
        where T : class, new()
    {
        builder.Services.AddSingleton(new T());

        builder.Add<LiveState<T>>()
               .As<ILiveState<T>>()
               .As<IDeployAware>();

        return builder;
    }
}
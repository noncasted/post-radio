using Common;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Meta.Online;

public interface IOnlineListener : IViewableProperty<int>
{
}

public interface IOnlineTracker
{
    void Track(IReadOnlyLifetime lifetime);
}

public class OnlineTrackerQueueId : IDurableQueueId
{
    public string ToRaw() => "online-tracker";
}

[GenerateSerializer]
public class OnlineTrackerPayload
{
    [Id(0)] public required int Value { get; init; }
}

public class OnlineTracker : IOnlineTracker, ICoordinatorSetupCompleted
{
    public OnlineTracker(IMessaging messaging)
    {
        _messaging = messaging;
    }

    private readonly IMessaging _messaging;
    private readonly OnlineTrackerQueueId _queueId = new();

    private int _count;

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        Loop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public void Track(IReadOnlyLifetime lifetime)
    {
        Interlocked.Increment(ref _count);
        lifetime.Listen(() => Interlocked.Decrement(ref _count));
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        while (!lifetime.IsTerminated)
        {
            await _messaging.PushDirectQueue(_queueId, new OnlineTrackerPayload { Value = _count });
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
}

public class OnlineListener : ViewableProperty<int>, IOnlineListener, ICoordinatorSetupCompleted
{
    public OnlineListener(IMessaging messaging) : base(0)
    {
        _messaging = messaging;
    }

    private readonly IMessaging _messaging;
    private readonly OnlineTrackerQueueId _queueId = new();

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        _messaging.ListenDurableQueue<OnlineTrackerPayload>(lifetime, _queueId, payload => Set(payload.Value));
        return Task.CompletedTask;
    }
}

public static class OnlineServicesExtensions
{
    public static IHostApplicationBuilder AddOnlineServices(this IHostApplicationBuilder builder)
    {
        builder.Add<OnlineTracker>()
               .As<IOnlineTracker>()
               .As<ICoordinatorSetupCompleted>();

        builder.Add<OnlineListener>()
               .As<IOnlineListener>()
               .As<ICoordinatorSetupCompleted>();

        return builder;
    }
}
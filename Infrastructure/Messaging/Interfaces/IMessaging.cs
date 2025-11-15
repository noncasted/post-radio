using Common;
using Infrastructure.Discovery;

namespace Infrastructure.Messaging;

public interface IMessageQueueId
{
    string ToRaw();
}

public class MessageQueueId : IMessageQueueId
{
    public MessageQueueId(string id)
    {
        _id = id;
    }

    private readonly string _id;

    public string ToRaw()
    {
        return _id;
    }
}

public interface IMessagePipeId
{
    string ToRaw();
}

public class MessagePipeId : IMessagePipeId
{
    public MessagePipeId(string id)
    {
        _id = id;
    }

    private readonly string _id;

    public string ToRaw()
    {
        return _id;
    }
}

public class MessagePipeServiceRequestId : IMessagePipeId
{
    public MessagePipeServiceRequestId(IServiceOverview serviceOverview, Type type)
    {
        _serviceOverview = serviceOverview;
        _type = type;
    }

    private readonly IServiceOverview _serviceOverview;
    private readonly Type _type;

    public string ToRaw()
    {
        return $"service-pipe-request-{_serviceOverview.Id}-{_type.FullName}";
    }
}

public interface IMessaging
{
    IMessageQueueClient Queue { get; }
    IMessagePipeClient Pipe { get; }

    Task Start(IReadOnlyLifetime lifetime);
}

public static class MessagingExtensions
{
    public static Task PushTransactionalQueue(this IMessaging messaging, IMessageQueueId id, object message)
    {
        return messaging.Queue.PushTransactional(id, message);
    }

    public static Task PushDirectQueue(this IMessaging messaging, IMessageQueueId id, object message)
    {
        return messaging.Queue.PushDirect(id, message);
    }

    public static void ListenQueue<T>(
        this IMessaging messaging,
        IReadOnlyLifetime lifetime,
        IMessageQueueId id,
        Action<T> listener)
    {
        var consumer = messaging.Queue.GetOrCreateConsumer<T>(id);
        consumer.Advise(lifetime, listener);
    }

    public static async Task ListenPipe<T>(
        this IMessaging messaging,
        IReadOnlyLifetime lifetime,
        IMessagePipeId id,
        Action<T> listener)
    {
        var consumer = await messaging.Pipe.GetOrCreateConsumer<T>(id);
        consumer.Advise(lifetime, listener);
    }

    public static Task AddPipeRequestHandler<TRequest, TResponse>(
        this IMessaging messaging,
        IReadOnlyLifetime lifetime,
        IMessagePipeId id,
        Func<TRequest, Task<TResponse>> listener)
    {
        return messaging.Pipe.AddHandler(lifetime, id, listener);
    }

    public static Task SendPipe(this IMessaging messaging, IMessagePipeId id, object message)
    {
        return messaging.Pipe.Send(id, message);
    }

    public static Task<TResponse> SendPipe<TResponse>(this IMessaging messaging, IMessagePipeId id, object message)
    {
        return messaging.Pipe.Send<TResponse>(id, message);
    }
}
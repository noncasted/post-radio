using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public interface IDurableQueueId
{
    string ToRaw();
}

public class DurableQueueId : IDurableQueueId
{
    public DurableQueueId(string id)
    {
        _id = id;
    }

    private readonly string _id;

    public string ToRaw()
    {
        return _id;
    }
}

public interface IRuntimePipeId
{
    string ToRaw();
}

public class RuntimePipeId : IRuntimePipeId
{
    public RuntimePipeId(string id)
    {
        _id = id;
    }

    private readonly string _id;

    public string ToRaw()
    {
        return _id;
    }
}

public static class MessagingExtensions
{
    extension(IMessaging messaging)
    {
        public void PushTransactionalQueue(IDurableQueueId id, object message)
        {
            messaging.DurableQueue.PushTransactional(id, message);
        }

        public Task PushDirectQueue(IDurableQueueId id, object message)
        {
            return messaging.DurableQueue.PushDirect(id, message);
        }

        public async Task ListenDurableQueue<T>(
            IReadOnlyLifetime lifetime,
            IDurableQueueId id,
            Action<T> listener)
        {
            var consumer = await messaging.DurableQueue.GetOrCreateConsumer<T>(id);
            consumer.Advise(lifetime, listener);
        }

        public Task AddPipeRequestHandler<TRequest, TResponse>(
            IReadOnlyLifetime lifetime,
            IRuntimePipeId id,
            Func<TRequest, Task<TResponse>> listener)
        {
            return messaging.RuntimePipe.AddHandler(lifetime, id, listener);
        }

        public Task<TResponse> SendPipe<TResponse>(IRuntimePipeId id, object message)
        {
            return messaging.RuntimePipe.Send<TResponse>(id, message);
        }

        public Task<bool> IsPipeExists(IRuntimePipeId id)
        {
            return messaging.RuntimePipe.Exists(id);
        }

        public Task PublishChannel(IRuntimeChannelId id, object message)
        {
            return messaging.RuntimeChannel.Publish(id, message);
        }

        public async Task ListenChannel<T>(
            IReadOnlyLifetime lifetime,
            IRuntimeChannelId id,
            Action<T> listener,
            Action? onGapDetected = null)
        {
            var consumer = await messaging.RuntimeChannel.GetOrCreateConsumer<T>(id, onGapDetected);
            consumer.Advise(lifetime, listener);
        }
    }

    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder)
    {
        builder.Add<Messaging>()
               .As<IMessaging>();

        builder.Add<DurableQueueClient>()
               .As<IDurableQueueClient>();

        builder.Add<RuntimePipeClient>()
               .As<IRuntimePipeClient>();

        builder.Add<RuntimeChannelClient>()
               .As<IRuntimeChannelClient>();

        return builder;
    }
}
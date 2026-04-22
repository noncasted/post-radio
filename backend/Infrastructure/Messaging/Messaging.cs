using Common.Reactive;

namespace Infrastructure;

public interface IMessaging
{
    IDurableQueueClient DurableQueue { get; }
    IRuntimePipeClient RuntimePipe { get; }
    IRuntimeChannelClient RuntimeChannel { get; }

    Task Start(IReadOnlyLifetime lifetime);
}

public class Messaging : IMessaging
{
    public Messaging(
        IDurableQueueClient durableQueue,
        IRuntimePipeClient runtimePipe,
        IRuntimeChannelClient runtimeChannel)
    {
        DurableQueue = durableQueue;
        RuntimePipe = runtimePipe;
        RuntimeChannel = runtimeChannel;
    }

    public IDurableQueueClient DurableQueue { get; }
    public IRuntimePipeClient RuntimePipe { get; }
    public IRuntimeChannelClient RuntimeChannel { get; }

    public Task Start(IReadOnlyLifetime lifetime)
    {
        return Task.WhenAll(DurableQueue.Start(lifetime),
            RuntimePipe.Start(lifetime),
            RuntimeChannel.Start(lifetime));
    }
}
using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Messaging;

public class Messaging : IMessaging
{
    public Messaging(IMessageQueueClient queue, IMessagePipeClient pipe)
    {
        Queue = queue;
        Pipe = pipe;
    }

    public IMessageQueueClient Queue { get; }
    public IMessagePipeClient Pipe { get; }

    public Task Start(IReadOnlyLifetime lifetime)
    {
        return Task.WhenAll(Queue.Start(lifetime), Pipe.Start(lifetime));
    }
}

public static class MessagingExtensions
{
    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddSingleton<IMessaging, Messaging>();

        services.Add<MessageQueueClient>()
            .As<IMessageQueueClient>();

        services.Add<MessagePipeClient>()
            .As<IMessagePipeClient>();

        return builder;
    }
}
using Common;

namespace Infrastructure.Messaging;

public interface IMessagePipeClient
{
    Task Start(IReadOnlyLifetime lifetime);
    
    Task Send(IMessagePipeId id, object message);
    Task<TResponse> Send<TResponse>(IMessagePipeId id, object message);
    Task<IViewableDelegate<T>> GetOrCreateConsumer<T>(IMessagePipeId id);
    Task AddHandler<TRequest, TResponse>(
        IReadOnlyLifetime lifetime, 
        IMessagePipeId id,
        Func<TRequest, Task<TResponse>> listener);
}
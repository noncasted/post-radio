namespace Infrastructure.Messaging;

public interface IMessagePipeObserver : IGrainObserver
{
    Task Send(object message);
    Task<TResponse> Send<TResponse>(object message);
}
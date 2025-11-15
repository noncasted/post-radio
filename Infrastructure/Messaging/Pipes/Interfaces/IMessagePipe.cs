namespace Infrastructure.Messaging;

public interface IMessagePipe : IGrainWithStringKey
{
    Task BindObserver(IMessagePipeObserver observer);
    Task Send(object message);
    Task<TResponse> Send<TResponse>(object message);
}
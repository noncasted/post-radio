using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging;

public class MessagePipeObserver : IMessagePipeObserver
{
    public MessagePipeObserver(ILogger logger)
    {
        _logger = logger;
    }

    private readonly ILogger _logger;

    private Action<object>? _oneWayHandler;
    private Func<object, Task<object>>? _responseHandler;

    public Task Send(object message)
    {
        _logger.LogDebug("[Messaging] [Pipe] Processing one-way message {MessageType}", message.GetType().Name);

        if (_oneWayHandler == null)
        {
            _logger.LogWarning(
                "[Messaging] [Pipe] No one-way handler bound to process message of type {MessageType}",
                message.GetType().Name
            );

            return Task.CompletedTask;
        }

        try
        {
            _oneWayHandler(message);
            _logger.LogDebug(
                "[Messaging] [Pipe] Successfully processed one-way message {MessageType}",
                message.GetType().Name
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[Messaging] [Pipe] Failed to process one-way message {MessageType}",
                message.GetType().Name
            );
            throw;
        }

        return Task.CompletedTask;
    }

    public async Task<TResponse> Send<TResponse>(object message)
    {
        _logger.LogDebug(
            "[Messaging] [Pipe] Processing request-response message {MessageType} expecting {ResponseType}",
            message.GetType().Name,
            typeof(TResponse).Name
        );

        if (_responseHandler == null)
        {
            _logger.LogError(
                "[Messaging] [Pipe] No response handler bound to process message {MessageType}",
                message.GetType().Name
            );
            throw new InvalidOperationException("[Messaging] [Pipe] No response handler bound to process message.");
        }

        try
        {
            var response = await _responseHandler(message);

            if (response is not TResponse typedResponse)
            {
                _logger.LogError(
                    "[Messaging] [Pipe] Response type mismatch for message {MessageType}. Expected {ExpectedType}, but got {ActualType}",
                    message.GetType().Name,
                    typeof(TResponse).Name,
                    response.GetType().Name
                );
                throw new InvalidCastException($"Expected {typeof(TResponse)}, but got {response.GetType()}");
            }

            _logger.LogDebug(
                "[Messaging] [Pipe] Successfully processed request-response message {MessageType} with response {ResponseType}",
                message.GetType().Name,
                typeof(TResponse).Name
            );

            return typedResponse;
        }
        catch (Exception ex) when (!(ex is InvalidCastException))
        {
            _logger.LogError(
                ex,
                "[Messaging] [Pipe] Failed to process request-response message {MessageType}",
                message.GetType().Name
            );
            throw;
        }
    }

    public void BindOneWayHandler(Action<object> handler)
    {
        _logger.LogDebug("[Messaging] [Pipe] Binding one-way handler");

        if (_oneWayHandler != null)
        {
            _logger.LogWarning("[Messaging] [Pipe] Attempted to bind one-way handler when already bound");
            throw new InvalidOperationException("One-way handler is already bound.");
        }

        _oneWayHandler = handler;
        _logger.LogInformation("[Messaging] [Pipe] One-way handler bound successfully");
    }

    public void BindResponseHandler(Func<object, Task<object>> handler)
    {
        _logger.LogDebug("[Messaging] [Pipe] Binding response handler");

        if (_responseHandler != null)
        {
            _logger.LogWarning("[Messaging] [Pipe] Attempted to bind response handler when already bound");
            throw new InvalidOperationException("Response handler is already bound.");
        }

        _responseHandler = handler;
        _logger.LogInformation("[Messaging] [Pipe] Response handler bound successfully");
    }
}
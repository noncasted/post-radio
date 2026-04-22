using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IRuntimePipeObserver : IGrainObserver
{
    Task<TResponse> Send<TResponse>(object message);
}

public class RuntimePipeObserver : IRuntimePipeObserver
{
    public RuntimePipeObserver(ILogger logger)
    {
        _logger = logger;
    }

    private readonly ILogger _logger;

    private Func<object, Task<object>>? _responseHandler;

    public void BindResponseHandler(Func<object, Task<object>> handler)
    {
        _logger.LogTrace("[Messaging] [RuntimePipe] Binding response handler");

        if (_responseHandler != null)
        {
            _logger.LogWarning("[Messaging] [RuntimePipe] Attempted to bind response handler when already bound");
            throw new InvalidOperationException("Response handler is already bound.");
        }

        _responseHandler = handler;
        _logger.LogTrace("[Messaging] [RuntimePipe] Response handler bound successfully");
    }

    public async Task<TResponse> Send<TResponse>(object message)
    {
        _logger.LogTrace(
            "[Messaging] [RuntimePipe] Processing request-response message {MessageType} expecting {ResponseType}",
            message.GetType().Name, typeof(TResponse).Name);

        if (_responseHandler == null)
        {
            _logger.LogError("[Messaging] [RuntimePipe] No response handler bound to process message {MessageType}",
                message.GetType().Name);

            throw new InvalidOperationException(
                "[Messaging] [RuntimePipe] No response handler bound to process message.");
        }

        try
        {
            var response = await _responseHandler(message);

            if (response is not TResponse typedResponse)
            {
                _logger.LogError(
                    "[Messaging] [RuntimePipe] Response type mismatch for message {MessageType}. Expected {ExpectedType}, but got {ActualType}",
                    message.GetType().Name, typeof(TResponse).Name, response.GetType().Name);
                throw new InvalidCastException($"Expected {typeof(TResponse)}, but got {response.GetType()}");
            }

            _logger.LogTrace(
                "[Messaging] [RuntimePipe] Successfully processed request-response message {MessageType} with response {ResponseType}",
                message.GetType().Name, typeof(TResponse).Name);

            return typedResponse;
        }
        catch (Exception ex) when (ex is not InvalidCastException)
        {
            _logger.LogError(ex, "[Messaging] [RuntimePipe] Failed to process request-response message {MessageType}",
                message.GetType().Name);
            throw;
        }
    }
}
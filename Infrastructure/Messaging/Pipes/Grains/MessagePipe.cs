using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging;

public class MessagePipe : Grain, IMessagePipe
{
    public MessagePipe(ILogger<MessagePipe> logger)
    {
        _logger = logger;
    }

    private readonly ILogger<MessagePipe> _logger;

    private IMessagePipeObserver? _observer;
    private DateTime _setDate;

    public Task BindObserver(IMessagePipeObserver observer)
    {
        _logger.LogDebug("[Messaging] [Pipe] Binding observer to pipe {PipeId}", this.GetPrimaryKeyString());
        _observer = observer;
        _setDate = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public async Task Send(object message)
    {
        _logger.LogDebug(
            "[Messaging] [Pipe] Sending one-way message {MessageType} to pipe {PipeId}",
            message.GetType().Name,
            this.GetPrimaryKeyString()
        );

        if (_observer == null)
        {
            _logger.LogWarning(
                "[Messaging] [Pipe] Dropping message {MessageType} for pipe {PipeId} because no observer is bound",
                message.GetType().Name,
                this.GetPrimaryKeyString()
            );
            return;
        }

        try
        {
            await _observer!.Send(message);

            _logger.LogDebug(
                "[Messaging] [Pipe] Successfully sent one-way message {MessageType} to pipe {PipeId}",
                message.GetType().Name,
                this.GetPrimaryKeyString()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[Messaging] [Pipe] Failed to send one-way message {MessageType} to pipe {PipeId}",
                message.GetType().Name,
                this.GetPrimaryKeyString()
            );
            throw;
        }
    }

    public async Task<TResponse> Send<TResponse>(object message)
    {
        _logger.LogDebug(
            "[Messaging] [Pipe] Sending request-response message {MessageType} expecting {ResponseType} to pipe {PipeId}",
            message.GetType().Name,
            typeof(TResponse).Name,
            this.GetPrimaryKeyString()
        );

        if (_observer == null)
        {
            _logger.LogError(
                "[Messaging] [Pipe] No observer bound for request-response message {MessageType} on pipe {PipeId}",
                message.GetType().Name,
                this.GetPrimaryKeyString()
            );
            throw new Exception($"No observer for stream {this.GetPrimaryKeyString()}");
        }

        try
        {
            var response = await _observer!.Send<TResponse>(message);
            _logger.LogDebug(
                "[Messaging] [Pipe] Successfully received response {ResponseType} for message {MessageType} on pipe {PipeId}",
                typeof(TResponse).Name,
                message.GetType().Name,
                this.GetPrimaryKeyString()
            );
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[Messaging] [Pipe] Failed to process request-response message {MessageType} on pipe {PipeId}",
                message.GetType().Name,
                this.GetPrimaryKeyString()
            );
            throw;
        }
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        var timeSinceLastUpdate = DateTime.UtcNow - _setDate;

        if (timeSinceLastUpdate > TimeSpan.FromMinutes(3))
            return;

        throw new Exception("[Messaging] [Pipe ] Keeping pipe alive because observer was recently set");
    }
}
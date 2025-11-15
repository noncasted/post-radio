using System.Net.WebSockets;
using System.Threading.Channels;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Shared;

namespace Common;

/// <summary>
/// Backend: WriteOneWay -> Client
/// Backend: WriteRequest -> Client: WriteResponse -> Backend: OnRequestHandled
/// Client: WriteRequest -> Backend: WriteResponse -> Client: OnRequestHandled
/// </summary>
public interface IConnectionWriter
{
    ValueTask WriteOneWay(INetworkContext context);
    Task<INetworkContext?> WriteRequest<T>(INetworkContext context) where T : INetworkContext;
    ValueTask WriteResponse(INetworkContext context, int requestId);
    void OnRequestHandled(INetworkContext context, int requestId);
}

public class ConnectionWriter : IConnectionWriter
{
    public ConnectionWriter(WebSocket webSocket, ILogger logger)
    {
        _webSocket = webSocket;
        _logger = logger;
    }

    private readonly WebSocket _webSocket;
    private readonly ILogger _logger;

    private readonly Dictionary<int, TaskCompletionSource<INetworkContext?>> _pendingRequests = new();

    private readonly Channel<IMessageFromServer> _queue = Channel.CreateBounded<IMessageFromServer>(
        new BoundedChannelOptions(10)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = true
        });

    private int _requestId = 1_000_000;

    public async Task Run(IReadOnlyLifetime lifetime)
    {
        var reader = _queue.Reader;
        var buffer = new MemoryStream();
        var cancellation = lifetime.Token;
        
        lifetime.Listen(() =>
        {
            foreach (var (_, completion) in _pendingRequests)
                completion.TrySetCanceled();
        });

        while (IsAlive() == true)
        {
            var readResult = await reader.WaitToReadAsync(cancellation);

            if (readResult == false)
                break;

            while (reader.TryRead(out var message) == true)
            {
                if (IsAlive() == false)
                {
                    TryHandleRequestFail();
                    break;
                }

                try
                {
                    await MemoryPackSerializer.SerializeAsync(buffer, message, cancellationToken: cancellation);

                    var sendBuffer = new ReadOnlyMemory<byte>(buffer.GetBuffer(), 0, (int)buffer.Position);
                    await _webSocket.SendAsync(sendBuffer, WebSocketMessageType.Binary, true, lifetime.Token);
                    buffer.Position = 0;
                }
                catch (Exception e)
                {
                    TryHandleRequestFail();
                    _logger.LogError(e, "Error while sending message: {Message}", message.GetType().FullName);
                }

                void TryHandleRequestFail()
                {
                    if (message is not RequestMessageFromServer requestMessage)
                        return;

                    var requestId = requestMessage.RequestId;

                    if (_pendingRequests.TryGetValue(requestId, out var completion))
                    {
                        completion.SetResult(null);
                        _pendingRequests.Remove(requestId);
                    }
                }
            }
        }

        _queue.Writer.Complete();
        await buffer.DisposeAsync();

        bool IsAlive()
        {
            return _webSocket.State == WebSocketState.Open && lifetime.IsTerminated == false;
        }
    }

    public ValueTask WriteOneWay(INetworkContext context)
    {
        var response = new OneWayMessageFromServer()
        {
            Context = context
        };

        return _queue.Writer.WriteAsync(response);
    }

    public async Task<INetworkContext?> WriteRequest<T>(INetworkContext context) where T : INetworkContext
    {
        var response = new RequestMessageFromServer()
        {
            Context = context,
            RequestId = _requestId
        };

        _requestId++;

        var completion = new TaskCompletionSource<INetworkContext?>();
        _pendingRequests[response.RequestId] = completion;

        await _queue.Writer.WriteAsync(response);

        var result = await completion.Task;
        return result;
    }

    public ValueTask WriteResponse(INetworkContext context, int requestId)
    {
        var response = new ResponseMessageFromServer()
        {
            Context = context,
            RequestId = requestId
        };

        return _queue.Writer.WriteAsync(response);
    }

    public void OnRequestHandled(INetworkContext context, int requestId)
    {
        if (_pendingRequests.TryGetValue(requestId, out var completion))
        {
            completion.SetResult(context);
            _pendingRequests.Remove(requestId);
        }
        else
        {
            _logger.LogWarning("Response created for unknown request ID: {RequestId}", requestId);
        }
    }
}
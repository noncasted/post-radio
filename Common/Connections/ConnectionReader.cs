using System.Net.WebSockets;
using MemoryPack;
using Shared;

namespace Common;

public interface IConnectionReader
{
    IViewableDelegate<OneWayMessageFromClient> OneWay { get; }
    IViewableDelegate<RequestMessageFromClient> Requests { get; }
    IViewableDelegate<ResponseMessageFromClient> Responses { get; }
}

public class ConnectionReader : IConnectionReader
{
    public ConnectionReader(WebSocket webSocket)
    {
        _webSocket = webSocket;
    }

    private readonly WebSocket _webSocket;

    private readonly ViewableDelegate<OneWayMessageFromClient> _oneWay = new();
    private readonly ViewableDelegate<RequestMessageFromClient> _requests = new();
    private readonly ViewableDelegate<ResponseMessageFromClient> _responses = new();

    public IViewableDelegate<OneWayMessageFromClient> OneWay => _oneWay;
    public IViewableDelegate<RequestMessageFromClient> Requests => _requests;
    public IViewableDelegate<ResponseMessageFromClient> Responses => _responses;

    public async Task Run(IReadOnlyLifetime lifetime)
    {
        var buffer = new byte[1024 * 1024 * 4].AsMemory();

        while (_webSocket.State == WebSocketState.Open && lifetime.IsTerminated == false)
        {
            ValueWebSocketReceiveResult receiveResult;

            try
            {
                receiveResult = await _webSocket.ReceiveAsync(
                    buffer,
                    lifetime.Token
                );
            }
            catch (WebSocketException)
            {
                break;
            }

            if (_webSocket.CloseStatus != null)
                break;

            var payload = buffer[..receiveResult.Count];
            var context = MemoryPackSerializer.Deserialize<IMessageFromClient>(payload.Span)!;

            switch (context)
            {
                case OneWayMessageFromClient oneWay:
                    _oneWay.Invoke(oneWay);
                    break;
                case RequestMessageFromClient request:
                    _requests.Invoke(request);
                    break;
                case ResponseMessageFromClient response:
                    _responses.Invoke(response);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
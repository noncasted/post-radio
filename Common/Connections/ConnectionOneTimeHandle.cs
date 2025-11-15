using System.Net.WebSockets;
using MemoryPack;
using Shared;

namespace Common;

public class ConnectionOneTimeHandle : IDisposable
{
    public ConnectionOneTimeHandle(WebSocket socket)
    {
        _socket = socket;
    }

    private readonly WebSocket _socket;
    private readonly MemoryStream _writeBuffer = new();

    private Memory<byte> _readBuffer = new byte[1024 * 1024 * 4].AsMemory();
    private int _requestId;

    public async Task<T> ReadRequest<T>() where T : INetworkContext
    {
        var rawAuth = await _socket.ReceiveAsync(_readBuffer, CancellationToken.None);
        var payload = _readBuffer[..rawAuth.Count];
        var message = MemoryPackSerializer.Deserialize<IMessageFromClient>(payload.Span)!;

        if (message is not RequestMessageFromClient request)
        {
            throw new InvalidOperationException(
                $"Invalid request type: {message.GetType().Name}, expected: {nameof(RequestMessageFromClient)}");
        }

        if (request.Context is not T context)
        {
            throw new InvalidOperationException(
                $"Invalid request type: {request.Context.GetType().Name}, expected: {typeof(T).Name}");
        }

        _requestId = request.RequestId;

        return context;
    }

    public async Task SendResponse<T>(T context) where T : INetworkContext
    {
        var response = new ResponseMessageFromServer()
        {
            Context = context,
            RequestId = _requestId
        };
         
        await MemoryPackSerializer.SerializeAsync<IMessageFromServer>(_writeBuffer, response);
        var sendBuffer = new ReadOnlyMemory<byte>(_writeBuffer.GetBuffer(), 0, (int)_writeBuffer.Length);

        await _socket.SendAsync(sendBuffer, WebSocketMessageType.Binary, true, CancellationToken.None);
        _writeBuffer.SetLength(0);
    }

    public void Dispose()
    {
        _readBuffer = Memory<byte>.Empty;
        _writeBuffer.Dispose();
    }
}
using System.Net.WebSockets;
using System.Text;

namespace CryptoTrader.Exchanges.Okx.WebSocket;

internal interface IOkxSocket : IAsyncDisposable
{
    WebSocketState State { get; }

    Task SendTextAsync(string text, CancellationToken cancellationToken);

    ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken);

    void Abort();
}

internal interface IOkxSocketConnector
{
    Task<IOkxSocket> ConnectAsync(Uri uri, CancellationToken cancellationToken);
}

internal sealed class ClientWebSocketConnector : IOkxSocketConnector
{
    public async Task<IOkxSocket> ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();
        try
        {
            await socket.ConnectAsync(uri, cancellationToken);
            return new ClientSocket(socket);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private sealed class ClientSocket : IOkxSocket
    {
        private readonly ClientWebSocket _socket;

        public ClientSocket(ClientWebSocket socket) => _socket = socket;

        public WebSocketState State => _socket.State;

        public Task SendTextAsync(string text, CancellationToken cancellationToken)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            return _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }

        public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            _socket.ReceiveAsync(buffer, cancellationToken);

        public void Abort()
        {
            try
            {
                _socket.Abort();
            }
            catch (WebSocketException)
            {
            }
        }

        public ValueTask DisposeAsync()
        {
            _socket.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

internal readonly record struct OkxChannelSubscription(string Channel, string InstId)
{
    public string Key => Channel + "\u001f" + InstId;
}

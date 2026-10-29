using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace OnlineDungeonServer.Realtime;

internal static class DungeonRealtimeWebSocketTunnel
{
    private const int WebSocketBufferSize = 8 * 1024;

    public static async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("WebSocket request required.");
            return;
        }

        int realtimePort = context.RequestServices
            .GetRequiredService<IConfiguration>()
            .GetValue("Realtime:UdpPort", DungeonRealtimeProtocol.DefaultPort);
        ILogger logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DungeonRealtimeWebSocketTunnel");

        using WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
        using var udpClient = new UdpClient(AddressFamily.InterNetwork);
        udpClient.Connect(IPAddress.Loopback, realtimePort);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);

        Task webSocketToUdp = RelayWebSocketToUdpAsync(webSocket, udpClient, cancellation.Token);
        Task udpToWebSocket = RelayUdpToWebSocketAsync(webSocket, udpClient, cancellation.Token);

        try
        {
            await Task.WhenAny(webSocketToUdp, udpToWebSocket);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Realtime WebSocket tunnel closed with an error.");
        }
        finally
        {
            cancellation.Cancel();
            udpClient.Close();

            try
            {
                await Task.WhenAll(webSocketToUdp, udpToWebSocket);
            }
            catch (Exception) when (cancellation.IsCancellationRequested)
            {
            }

            if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Realtime tunnel closed", CancellationToken.None);
                }
                catch (WebSocketException)
                {
                }
            }
        }
    }

    private static async Task RelayWebSocketToUdpAsync(WebSocket webSocket, UdpClient udpClient, CancellationToken cancellationToken)
    {
        byte[] receiveBuffer = new byte[WebSocketBufferSize];
        using var messageBuffer = new MemoryStream(DungeonRealtimeProtocol.MaxReliableMessageBytes);

        while (!cancellationToken.IsCancellationRequested && webSocket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(receiveBuffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
                return;

            if (result.MessageType != WebSocketMessageType.Binary && result.MessageType != WebSocketMessageType.Text)
                continue;

            messageBuffer.SetLength(0);
            while (true)
            {
                if (result.Count > 0)
                    messageBuffer.Write(receiveBuffer, 0, result.Count);

                if (messageBuffer.Length > DungeonRealtimeProtocol.MaxReliableMessageBytes)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Realtime message too large", cancellationToken);
                    return;
                }

                if (result.EndOfMessage)
                    break;

                result = await webSocket.ReceiveAsync(receiveBuffer, cancellationToken);
            }

            byte[] datagram = messageBuffer.ToArray();
            if (datagram.Length > 0)
                await udpClient.SendAsync(datagram, cancellationToken);
        }
    }

    private static async Task RelayUdpToWebSocketAsync(WebSocket webSocket, UdpClient udpClient, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && webSocket.State == WebSocketState.Open)
        {
            UdpReceiveResult result = await udpClient.ReceiveAsync(cancellationToken);
            if (result.Buffer.Length <= 0)
                continue;

            await webSocket.SendAsync(
                result.Buffer,
                WebSocketMessageType.Binary,
                true,
                cancellationToken);
        }
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace BlazorMauiCssLiveReload
{
    internal class EmbeddedCssWebSocketServer : IEmbeddedCssServer
    {
        private readonly List<WebSocket> _clients = new();
        private HttpListener? _httpListener;
        private CancellationTokenSource? _cts;
        private readonly CssLiveReloadOptions _options;
        private readonly ILogger<EmbeddedCssWebSocketServer> _logger;
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public EmbeddedCssWebSocketServer(IOptions<CssLiveReloadOptions> options,
            ILogger<EmbeddedCssWebSocketServer> logger)
        {
            _options = options.Value;
            _logger = logger;   
        }

        public async Task StartAsync()
        {
            _cts = new CancellationTokenSource();
            _httpListener = new HttpListener();

            string listenUrl = $"http://{_options.ListenerHost}:{_options.ListenerPort}/";

            _logger.LogInformation("CSS WebSocket server listening on {Url}", listenUrl);

            _httpListener.Prefixes.Add(listenUrl);
            _httpListener.Start();

            _ = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        var context = await _httpListener.GetContextAsync();
                        if (context.Request.IsWebSocketRequest)
                        {
                            var wsContext = await context.AcceptWebSocketAsync(null);
                            var ws = wsContext.WebSocket;
                            lock (_clients) { _clients.Add(ws); }
                            _logger.LogInformation("New WebSocket client connected. Total clients: {Count}", _clients.Count);
                            _ = Listen(ws);
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                            context.Response.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in WebSocket listener loop");
                    }
                }
            });
        }

        private async Task Listen(WebSocket ws)
        {
            var buffer = new byte[1024];
            try
            {
                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                }
            }
            finally
            {
                lock (_clients) { _clients.Remove(ws); }
                _logger.LogInformation("WebSocket client disconnected. Total clients: {Count}", _clients.Count);
            }
        }

        public async Task BroadcastCssAsync(string css, bool isScoped = false)
        {
            var message = (isScoped ? "SCOPED:" : "NORMAL:") + css;
            var bytes = Encoding.UTF8.GetBytes(message);

            List<WebSocket> clients;
            lock (_clients) { clients = _clients.ToList(); }

            await _sendLock.WaitAsync();
            try
            {
                foreach (var ws in clients)
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        try
                        {
                            await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                            _logger.LogInformation("Sent CSS update ({Length} chars, scoped={Scoped})", css.Length, isScoped);
                        }
                        catch (Exception ex)
                        {
                            lock (_clients) { _clients.Remove(ws); }
                            _logger.LogWarning(ex, "Error sending CSS to client, removed from list");
                        }
                    }
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }
}

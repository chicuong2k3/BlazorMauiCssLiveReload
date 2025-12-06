using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace BlazorMauiCssLiveReload
{
    public class CssLiveReloadService
    {
        private IJSRuntime? _jsRuntime;
        private readonly CssLiveReloadOptions _options;
        private readonly ILogger<CssLiveReloadService> _logger;

        public CssLiveReloadService(
            IOptions<CssLiveReloadOptions> options,
            ILogger<CssLiveReloadService> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task ConnectAsync(IJSRuntime jsRuntime)
        {
            try
            {
                _jsRuntime = jsRuntime;
                _logger.LogInformation("CssLiveReloadService: Attempting to connect to {ServerUrl}", _options.ServerUrl);
                
                await WaitForJsRuntimeReadyAsync();
                
                await _jsRuntime.InvokeVoidAsync("BlazorMauiCssLiveReload.start", _options.ServerUrl);
                
                _logger.LogInformation("CssLiveReloadService: Successfully called JS start method");
            }
            catch (JSDisconnectedException ex)
            {
                _logger.LogError(ex, "JSRuntime disconnected while connecting to CSS Live Reload server.");
            }
            catch (JSException ex)
            {
                _logger.LogError(ex, "JS Exception: {Message}. Object may not be initialized yet.", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to CSS Live Reload server.");
            }
        }

        private async Task WaitForJsRuntimeReadyAsync()
        {
            const int maxAttempts = 50;
            const int delayMs = 100;

            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    await _jsRuntime!.InvokeAsync<bool>("eval", "!!window.BlazorMauiCssLiveReload");
                    return;
                }
                catch
                {
                    if (i < maxAttempts - 1)
                    {
                        await Task.Delay(delayMs);
                    }
                }
            }

            _logger.LogWarning("CssLiveReloadService: JS runtime did not fully initialize after {Delay}ms", maxAttempts * delayMs);
        }
    }
}

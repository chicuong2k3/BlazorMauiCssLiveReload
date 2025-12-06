namespace BlazorMauiCssLiveReload
{
    public interface IEmbeddedCssServer
    {
        Task StartAsync();
        Task BroadcastCssAsync(string css, bool isScoped = false);
    }
}

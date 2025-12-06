namespace BlazorMauiCssLiveReload
{
    public class CssLiveReloadOptions
    {
        public int ListenerPort { get; set; } = 5002;
        public string ListenerHost { get; set; } = "localhost";
        public string ServerUrl { get; set; } = "ws://localhost:5002/";
    }
}

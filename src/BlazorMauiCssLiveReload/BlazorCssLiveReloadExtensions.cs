using Microsoft.Extensions.DependencyInjection;

namespace BlazorMauiCssLiveReload
{
    public static class BlazorCssLiveReloadExtensions
    {
        public static IServiceCollection AddCssLiveReload(
            this IServiceCollection services,
            Action<CssLiveReloadOptions>? configureOptions = null)
        {
            if (configureOptions != null)
                services.Configure(configureOptions);
            else
                services.Configure<CssLiveReloadOptions>(options => { });

            services.AddSingleton<CssLiveReloadService>();
            services.AddSingleton<ScopedCssCompiler>();
            services.AddSingleton<CssChangeProcessor>();
            services.AddSingleton<IEmbeddedCssServer, EmbeddedCssWebSocketServer>();
            services.AddSingleton<CssFileWatcher>();

            return services;
        }

    }
}
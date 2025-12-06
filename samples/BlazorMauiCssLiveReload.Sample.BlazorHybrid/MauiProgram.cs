using Microsoft.Extensions.Logging;
using Serilog;
using BlazorMauiCssLiveReload;

namespace BlazorMauiCssLiveReload.Sample.BlazorHybrid;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File("Logs/log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();

#endif
        builder.Logging.AddDebug();
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog();

        builder.Services.AddMasaBlazor();

        builder.Services.AddCssLiveReload(options =>
        {
            options.ListenerPort = 5002;
            options.ListenerHost = "localhost";
            options.ServerUrl = "ws://localhost:5002/";
        });

        var app = builder.Build();

        // Fire-and-forget to initialize services without blocking app startup
        _ = InitializeBackgroundServicesAsync(app);

        return app;
    }

    private static async Task InitializeBackgroundServicesAsync(MauiApp app)
    {
        try
        {
            // Start WebSocket server first, then file watcher
            var server = app.Services.GetRequiredService<IEmbeddedCssServer>();
            await server.StartAsync();

            var watcher = app.Services.GetRequiredService<CssFileWatcher>();
            watcher.Start(FindProjectRoot(AppContext.BaseDirectory));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing background services: {ex}");
        }
    }

    private static string FindProjectRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);

        while (dir != null && dir.Exists)
        {
            if (dir.GetFiles("*.csproj").Any())
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new Exception("Cannot find project root (folder containing .csproj)");
    }
}


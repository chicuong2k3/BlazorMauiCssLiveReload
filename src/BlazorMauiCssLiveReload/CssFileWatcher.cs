using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace BlazorMauiCssLiveReload;

public class CssFileWatcher
{
    private readonly ILogger<CssFileWatcher> _logger;
    private readonly CssChangeProcessor _processor;

    private readonly ConcurrentQueue<string> _changeQueue = new(); 
    private readonly object _timerLock = new();
    private Timer? _debounceTimer;                             

    private readonly List<FileSystemWatcher> _watchers = new();

    public CssFileWatcher(
        CssChangeProcessor processor,
        ILogger<CssFileWatcher> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    public void Start(string projectRoot)
    {
        if (!Directory.Exists(projectRoot))
            throw new DirectoryNotFoundException(projectRoot);

        var watcher = new FileSystemWatcher(projectRoot)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite
                         | NotifyFilters.FileName
                         | NotifyFilters.Size,
            Filter = "*.css",
            EnableRaisingEvents = true
        };

        watcher.Changed += (_, e) => OnFileChanged(e.FullPath, projectRoot);
        watcher.Created += (_, e) => OnFileChanged(e.FullPath, projectRoot);
        watcher.Renamed += (_, e) => OnFileChanged(e.FullPath, projectRoot);

        _watchers.Add(watcher);

        _logger.LogInformation("CSS watcher active at {Root}", projectRoot);
    }

    private void OnFileChanged(string file, string projectRoot)
    {
        if (!file.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
            return;

        _changeQueue.Enqueue(file);                       
        RestartTimer(projectRoot);                       
    }

    private void RestartTimer(string projectRoot)
    {
        lock (_timerLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(async _ => await ProcessBatch(projectRoot),
                null,
                200,                                    
                Timeout.Infinite);
        }
    }

    private async Task ProcessBatch(string projectRoot)
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (_changeQueue.TryDequeue(out var f))
            unique.Add(f);

        if (unique.Count == 0) return;

        _logger.LogInformation("Processing {Count} changed CSS files...", unique.Count);

        await _processor.ProcessFiles(unique, projectRoot);            
    }
}

using Microsoft.Extensions.Logging;

namespace BlazorMauiCssLiveReload;

public class CssChangeProcessor
{
    private readonly IEmbeddedCssServer _server;
    private readonly ScopedCssCompiler _scopedCompiler;
    private readonly ILogger<CssChangeProcessor> _logger;

    public CssChangeProcessor(
        IEmbeddedCssServer server,
        ScopedCssCompiler scopedCompiler,
        ILogger<CssChangeProcessor> logger)
    {
        _server = server;
        _scopedCompiler = scopedCompiler;
        _logger = logger;
    }

    public async Task ProcessFiles(IEnumerable<string> files, string projectRoot)
    {
        var normalCssFiles = new List<string>();
        var razorCssFiles = new List<string>();

        foreach (var file in files)
        {
            if (file.EndsWith(".razor.css", StringComparison.OrdinalIgnoreCase))
                razorCssFiles.Add(file);
            else
                normalCssFiles.Add(file);
        }

        foreach (var file in normalCssFiles)
        {
            await BroadcastNormalCss(file);
        }

        if (razorCssFiles.Count > 0)
        {
            _logger.LogInformation("🔄 Scoped CSS files changed: {Files}",
                string.Join(", ", razorCssFiles.Select(Path.GetFileName)));

            var beforeTime = DateTime.UtcNow;
            await TriggerDotnetRecompilation(razorCssFiles);

            var compiledCss = await WaitForCompiledCssAsync(projectRoot, beforeTime);

            if (compiledCss != null)
            {
                await _server.BroadcastCssAsync(compiledCss, isScoped: true);
                _logger.LogInformation("✓ Updated scoped CSS from compiled bundle");
            }
            else
            {
                _logger.LogWarning("⚠️  Could not load compiled scoped CSS. Ensure 'dotnet watch' is running.");
            }
        }
    }

    /// <summary>
    /// Waits for dotnet watch to recompile and produce a newer *.styles.css file.
    /// This ensures we get the updated CSS, not the stale version.
    /// </summary>
    private async Task<string?> WaitForCompiledCssAsync(string projectRoot, DateTime triggerTime)
    {
        var maxWaitTime = TimeSpan.FromSeconds(10);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var lastKnownFile = _scopedCompiler.FindStylesCss(projectRoot);
        var lastKnownModifyTime = lastKnownFile != null ? File.GetLastWriteTimeUtc(lastKnownFile) : DateTime.MinValue;

        while (stopwatch.Elapsed < maxWaitTime)
        {
            await Task.Delay(100);

            var currentFile = _scopedCompiler.FindStylesCss(projectRoot);
            if (currentFile == null)
                continue;

            var currentModifyTime = File.GetLastWriteTimeUtc(currentFile);

            // Check if file was modified AFTER we triggered recompilation
            if (currentModifyTime > triggerTime)
            {
                _logger.LogInformation("✓ Detected new compiled styles.css (modified: {Time})", currentModifyTime);
                var compiledCss = await _scopedCompiler.GetCompiledScopedCssAsync(projectRoot);
                if (compiledCss != null)
                    return compiledCss;
            }
        }

        _logger.LogWarning("⏱️  Timeout waiting for dotnet watch to compile (waited {Time}ms)", stopwatch.ElapsedMilliseconds);
        return null;
    }

    /// <summary>
    /// Triggers .NET watch recompilation by modifying the corresponding .razor file's timestamp.
    /// This forces dotnet watch to recompile, which regenerates *.styles.css bundle.
    /// </summary>
    private async Task TriggerDotnetRecompilation(List<string> razorCssFiles)
    {
        foreach (var cssFile in razorCssFiles)
        {
            var razorFile = cssFile.Substring(0, cssFile.Length - 4); // Remove .css extension

            if (!File.Exists(razorFile))
            {
                _logger.LogWarning("Corresponding .razor file not found: {File}", razorFile);
                continue;
            }

            try
            {
                File.SetLastWriteTimeUtc(razorFile, DateTime.UtcNow);
                _logger.LogInformation("Touched {File} to trigger .NET recompilation", Path.GetFileName(razorFile));
                await Task.Delay(50); 
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not touch {File}", razorFile);
            }
        }
    }

    private async Task BroadcastNormalCss(string file)
    {
        try
        {
            using var stream = new FileStream(file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);

            using var reader = new StreamReader(stream);
            var css = await reader.ReadToEndAsync();

            if (css.Length == 0) return;

            await _server.BroadcastCssAsync(css, isScoped: false);
            _logger.LogInformation("✓ Broadcasted normal CSS: {File}", Path.GetFileName(file));
        }
        catch (IOException)
        {
            _logger.LogWarning("Busy file skipped: {File}", file);
        }
    }
}

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MawMedia.Services;

public class CategoryDownloadCleaner
    : BackgroundService
{
    readonly ILogger _log;
    readonly CategoryDownloadConfig _config;

    public CategoryDownloadCleaner(
        ILogger<CategoryDownloadCleaner> log,
        IOptions<CategoryDownloadConfig> config
    )
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(config);

        _log = log;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _log.LogDebug("Starting to clean old download files");

            var dir = new DirectoryInfo(_config.RootDirectory);

            if (!dir.Exists)
            {
                _log.LogWarning("Download directory {DIRECTORY} does not exist!", _config.RootDirectory);
            }
            else
            {
                try
                {
                    Cleanup(dir);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(
                        "Ran into exception while trying to clean download directory {DIRECTORY}: {ERROR}",
                        _config.RootDirectory,
                        ex.Message
                    );
                }
            }

            _log.LogDebug("Waiting for {WAIT} minutes before next cleaning", _config.CleanIntervalInMinutes);

            await Task.Delay(TimeSpan.FromMinutes(_config.CleanIntervalInMinutes), stoppingToken);
        }
    }

    void Cleanup(DirectoryInfo dir)
    {
        var filesToRemove = dir
            .EnumerateFiles()
            .Where(ShouldDelete);

        foreach (var file in filesToRemove)
        {
            try
            {
                file.Delete();

                _log.LogInformation("Deleted file {FILE}", file.FullName);
            }
            catch (Exception ex)
            {
                _log.LogError(
                    ex,
                    "Could not delete file {FILE}: {ERROR}",
                    file.FullName,
                    ex.Message
                );
            }
        }
    }

    bool ShouldDelete(FileInfo file) =>
        file.LastWriteTime + TimeSpan.FromMinutes(_config.MinAgeBeforeDeleteInMinutes) < DateTime.Now;
}

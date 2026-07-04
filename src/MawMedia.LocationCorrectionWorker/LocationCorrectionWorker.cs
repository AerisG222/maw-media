using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MawMedia.LocationCorrectionWorker;

internal class LocationCorrectionWorker
    : BackgroundService
{
    readonly ILogger _log;
    readonly IServiceProvider _services;

    public LocationCorrectionWorker(
        ILogger<LocationCorrectionWorker> log,
        IServiceProvider services
    )
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(services);

        _log = log;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollTimeMinutes = 15;

        // seems like we try to run before our db is available and this causes the process to cancel, try adding a small delay to avoid
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        _log.LogInformation("LocationCorrectionWorker running at: {Time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var config = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<LocationCorrectionConfig>>();
                var fixer = scope.ServiceProvider.GetRequiredService<ILocationFixer>();

                pollTimeMinutes = config.Value.PollIntervalInMinutes;

                _log.LogInformation("Looking for locations to fix...");
                await fixer.FixAsync(stoppingToken);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "An error occurred while fixing locations.");
            }
            finally
            {
                if (!stoppingToken.IsCancellationRequested)
                {
                    _log.LogInformation("Pausing for {Interval} mins...", pollTimeMinutes);
                    await Task.Delay(TimeSpan.FromMinutes(pollTimeMinutes), stoppingToken);
                }
            }
        }

        _log.LogInformation("LocationCorrectionWorker stopping at: {Time}", DateTimeOffset.Now);
    }
}

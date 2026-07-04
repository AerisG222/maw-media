using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MawMedia.Services.Abstractions;

namespace MawMedia.LocationCorrectionWorker;

internal class LocationFixer
    : ILocationFixer
{
    readonly LocationCorrectionConfig _config;
    readonly IMediaRepository _repository;
    readonly ILogger<LocationFixer> _log;

    public LocationFixer(
        ILogger<LocationFixer> log,
        IOptionsSnapshot<LocationCorrectionConfig> config,
        IMediaRepository repository
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(repository);

        _config = config.Value;
        _log = log;
        _repository = repository;
    }

    public async Task FixAsync(CancellationToken stoppingToken = default)
    {
        // loop until we fix all missing locations as the func only returns a subset
        while(true)
        {
            var inaccurateLocations = await _repository.GetInaccurateLocations(_config.UserId, stoppingToken);

            if(!inaccurateLocations.Any())
            {
                _log.LogInformation("No inaccurate locations discovered.");
                return;
            }

            foreach (var location in inaccurateLocations)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    return;
                }

                var result = await _repository.FixInaccurateLocation(_config.UserId, location.MediaId, Guid.CreateVersion7(), stoppingToken);

                _log.LogInformation(BuildLogMessage(result), location.MediaId);

                await Task.Delay(TimeSpan.FromMilliseconds(_config.PerItemDelayInMillis), stoppingToken);
            }
        }
    }

    static string BuildLogMessage(int result) => result switch
    {
        1 => "Specified user is not an admin!",
        10 => "No location data found in exif for media {Media}",
        11 => "Media is already mapped to correct location for media {Media}",
        12 => "Media updated to point to an existing location for media {Media}",
        13 => "New location created and metadata copied from a nearby location for media {Media}",
        14 => "New location created for media {Media}",
        _ => "Unexpected result!"
    };
}

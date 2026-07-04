namespace MawMedia.LocationCorrectionWorker;

public interface ILocationFixer
{
    Task FixAsync(CancellationToken stoppingToken);
}

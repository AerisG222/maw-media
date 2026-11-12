namespace MawMedia.LocationCorrectionWorker;

public sealed class LocationCorrectionConfig
{
    public required int PollIntervalInMinutes { get; set; } = 15;
    public required int PerItemDelayInMillis { get; set; } = 10;
    public required Guid UserId { get; set; } = Guid.Empty;
}

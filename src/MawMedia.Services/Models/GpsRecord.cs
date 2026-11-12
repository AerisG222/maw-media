namespace MawMedia.Services.Models;

public sealed record GpsRecord(
    Guid MediaId,
    decimal? RecordedLatitude,
    decimal? RecordedLongitude,
    decimal? OverrideLatitude,
    decimal? OverrideLongitude
);

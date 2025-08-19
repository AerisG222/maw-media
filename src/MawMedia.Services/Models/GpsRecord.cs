namespace MawMedia.Services.Models;

public record GpsRecord(
    Guid MediaId,
    decimal RecordedLatitude,
    decimal RecordedLongitude,
    decimal OverrideLatitude,
    decimal OverrideLongitude
);

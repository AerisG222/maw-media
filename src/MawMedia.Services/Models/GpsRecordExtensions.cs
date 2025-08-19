using MawMedia.Models;

namespace MawMedia.Services.Models;

public static class GpsRecordExtensions
{
    public static Gps ToGps(this GpsRecord rec) =>
        new(
            rec.MediaId,
            new GpsCoordinate(rec.RecordedLatitude, rec.RecordedLongitude),
            new GpsCoordinate(rec.OverrideLatitude, rec.OverrideLongitude)
        );
}

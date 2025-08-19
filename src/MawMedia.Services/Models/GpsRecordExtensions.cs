using MawMedia.Models;

namespace MawMedia.Services.Models;

public static class GpsRecordExtensions
{
    public static Gps ToGps(this GpsRecord rec) =>
        new(
            rec.MediaId,
            BuildCoordinate(rec.RecordedLatitude, rec.RecordedLongitude),
            BuildCoordinate(rec.OverrideLatitude, rec.OverrideLongitude)
        );

    static GpsCoordinate? BuildCoordinate(decimal? lat, decimal? lng)
    {
        if (lat == null || lng == null)
        {
            return null;
        }

        return new((decimal)lat, (decimal)lng);
    }
}

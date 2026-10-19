namespace MawMedia.Services;

public class CategoryDownloadConfig
{
    public required string RootDirectory { get; set; }
    public int CleanIntervalInMinutes { get; set; }
    public int MinAgeBeforeDeleteInMinutes { get; set; }

    // limits on building archives.  serving one already on disk is never limited -
    // these only bound the disk and io a caller can make the service spend
    public int MaxConcurrentBuilds { get; set; } = 2;
    public int MaxBuildsPerUserPerHour { get; set; } = 10;
    public long MaxDirectorySizeInMB { get; set; } = 50_000;
}

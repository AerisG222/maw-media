namespace MawMedia.Services;

public class CategoryDownloadConfig
{
    public required string RootDirectory { get; set; }
    public int CleanIntervalInMinutes { get; set; }
    public int MinAgeBeforeDeleteInMinutes { get; set; }
}

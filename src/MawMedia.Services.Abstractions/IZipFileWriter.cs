namespace MawMedia.Services.Abstractions;

public interface IZipFileWriter
{
    Task<FileInfo?> GetZipFileIfExists(string filename);
    Task<ZipBuildResult> WriteZipFile(Guid userId, string filename, IEnumerable<string> filePaths, CancellationToken token = default);
}

public enum ZipBuildRefusal
{
    None,
    // this user has started too many builds recently
    UserRateLimited,
    // too many builds are already running
    Busy,
    // the download directory has no room for another archive
    OutOfSpace
}

public record ZipBuildResult(FileInfo? File, ZipBuildRefusal Refusal, TimeSpan RetryAfter)
{
    public static ZipBuildResult Built(FileInfo file) => new(file, ZipBuildRefusal.None, TimeSpan.Zero);
    public static ZipBuildResult Refused(ZipBuildRefusal refusal, TimeSpan retryAfter) => new(null, refusal, retryAfter);
}

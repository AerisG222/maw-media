using System.Text.RegularExpressions;
using MawMedia.Models;
using MawMedia.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MawMedia.Services;

public partial class UploadService
    : IUploadService
{
    const int MAX_FILENAME_LENGTH = 255;

    readonly ILogger _log;
    readonly UploadConfig _uploadConfig;

    public UploadService(
        ILogger<UploadService> log,
        IOptions<UploadConfig> config
    )
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(config);

        _log = log;
        _uploadConfig = config.Value;
    }

    public Task<IEnumerable<UploadedFile>> GetFiles(Guid userId)
    {
        var dir = BuildDir(userId);

        var files = dir.EnumerateFiles()
            .Select(f => new UploadedFile(f.Name, f.Length))
            .ToList()
            .AsEnumerable();

        return Task.FromResult(files);
    }

    public bool IsAcceptedFilename(string filename) =>
        !string.IsNullOrEmpty(filename)
        && filename.Length <= MAX_FILENAME_LENGTH
        && FilenamePattern().IsMatch(filename)
        // the pattern admits "." and "..", which name directories rather than a
        // file - the user's own upload directory and the root above it
        && filename.Trim('.', ' ').Length > 0;

    public async Task<UploadedFile?> UploadFile(Guid userId, Stream fileStream, string filename, CancellationToken token = default)
    {
        CheckFilename(filename);

        var file = BuildPhysicalFilename(userId, filename);
        FileStream fs;

        try
        {
            fs = new FileStream(file, FileMode.CreateNew);
        }
        catch (IOException) when (File.Exists(file))
        {
            return null;
        }

        try
        {
            await using (fs)
            {
                await fileStream.CopyToAsync(fs, token);
                await fs.FlushAsync(token);
            }
        }
        catch
        {
            // an upload cut short must not keep the name: the retry would be
            // refused as a duplicate of a file that never arrived whole
            File.Delete(file);
            throw;
        }

        var fi = new FileInfo(file);

        return new UploadedFile(Path.GetFileName(file), fi.Length);
    }

    public Task<string?> GetPhysicalFilePath(Guid userId, string filename)
    {
        if (!IsAcceptedFilename(filename))
        {
            return Task.FromResult((string?)null);
        }

        var file = BuildPhysicalFilename(userId, filename);

        if (!File.Exists(file))
        {
            return Task.FromResult((string?)null);
        }

        return Task.FromResult((string?)file);
    }

    DirectoryInfo BuildDir(Guid userId)
    {
        var dir = new DirectoryInfo(Path.Combine(_uploadConfig.RootDirectory, userId.ToString()));

        if (!dir.Exists)
        {
            dir.Create();
        }

        return dir;
    }

    string BuildPhysicalFilename(Guid userId, string filename)
    {
        var dir = BuildDir(userId);

        return Path.Combine(dir.FullName, filename);
    }

    void CheckFilename(string filename)
    {
        if (!IsAcceptedFilename(filename))
        {
            throw new ArgumentException("Filename not accepted, please try again.");
        }
    }

    [GeneratedRegex(@"^[0-9a-zA-Z_\-. ]+$")]
    private static partial Regex FilenamePattern();
}

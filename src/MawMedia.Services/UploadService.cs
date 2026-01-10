using System.Text.RegularExpressions;
using MawMedia.Models;
using MawMedia.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MawMedia.Services;

public class UploadService
    : IUploadService
{
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

    public async Task<UploadedFile> UploadFile(Guid userId, Stream fileStream, string filename)
    {
        CheckFilename(filename);

        var file = BuildPhysicalFilename(userId, filename);
        await using var fs = new FileStream(file, FileMode.CreateNew);
        await fileStream.CopyToAsync(fs);
        await fs.FlushAsync();
        fs.Close();

        var fi = new FileInfo(file);

        return new UploadedFile(Path.GetFileName(file), fi.Length);
    }

    public Task<string?> GetPhysicalFilePath(Guid userId, string filename)
    {
        CheckFilename(filename);

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

    static void CheckFilename(string filename)
    {
        if (!Regex.IsMatch(filename, @"^[0-9a-zA-Z_\-. ]+$"))
        {
            throw new ArgumentException("Filename not accepted, please try again.");
        }
    }
}

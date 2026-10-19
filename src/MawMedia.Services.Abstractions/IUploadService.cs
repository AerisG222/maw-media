using MawMedia.Models;

namespace MawMedia.Services.Abstractions;

public interface IUploadService
{
    Task<IEnumerable<UploadedFile>> GetFiles(Guid userId);
    bool IsAcceptedFilename(string filename);
    // null when this user already has a file by that name
    Task<UploadedFile?> UploadFile(Guid userId, Stream fileStream, string filename, CancellationToken token = default);
    Task<string?> GetPhysicalFilePath(Guid userId, string filename);
}

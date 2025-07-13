using MawMedia.Models;

namespace MawMedia.Services;

public interface IUploadService
{
    Task<IEnumerable<UploadedFile>> GetFiles(Guid userId);
    Task<UploadedFile> UploadFile(Guid userId, Stream fileStream, string filename);
    Task<string?> GetPhysicalFilePath(Guid userId, string filename);
}

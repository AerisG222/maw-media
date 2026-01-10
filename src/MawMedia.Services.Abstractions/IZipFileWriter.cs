namespace MawMedia.Services.Abstractions;

public interface IZipFileWriter
{
    Task<FileInfo?> GetZipFileIfExists(string filename);
    Task<FileInfo> WriteZipFile(string filename, IEnumerable<string> filePaths);
}

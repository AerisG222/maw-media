namespace MawMedia.Services;

public interface IZipFileWriter
{
    Task<FileInfo?> GetZipFileIfExists(string filename);
    Task<FileInfo> WriteZipFile(string filename, IEnumerable<string> filePaths);
}

namespace MawMedia.Services.Models;

class MediaAndFile
{
    public Guid Id { get; set; }
    public required string Type { get; set; }
    public required string FilePath { get; set; }
    public required string FileType { get; set; }
    public required string FileScale { get; set; }
    public bool MediaIsFavorite { get; set; }
}

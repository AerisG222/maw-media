namespace MawMedia.Services.Models;

class MediaAndFile
{
    public Guid MediaId { get; set; }
    public required string MediaType { get; set; }
    public bool MediaIsFavorite { get; set; }
    public required string FilePath { get; set; }
    public required string FileType { get; set; }
    public required string FileScale { get; set; }
}

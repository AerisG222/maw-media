namespace MawMedia.Services.Models;

// a rendition that may be published as a place cover.  media.get_place_cover_candidate
// never yields 'src', so nothing here can carry the original's exif.
class CoverCandidate
{
    public Guid FileId { get; set; }
    public required string FilePath { get; set; }
    public required string FileScale { get; set; }
    public required string FileType { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

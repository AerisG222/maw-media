namespace MawMedia.Models;

public record Media(
    Guid Id,
    Guid CategoryId,
    string Type,
    bool IsFavorite,
    IEnumerable<MediaFile> Files
);

namespace MawMedia.Models;

public record Media(
    Guid Id,
    string Slug,
    Guid CategoryId,
    string Type,
    bool IsFavorite,
    IEnumerable<MediaFile> Files
);

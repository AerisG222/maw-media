namespace MawMedia.Models;

public record Media(
    Guid Id,
    string Slug,
    Guid CategoryId,
    short CategoryYear,
    string CategorySlug,
    string Type,
    bool IsFavorite,
    IEnumerable<MediaFile> Files
);

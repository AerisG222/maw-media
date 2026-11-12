namespace MawMedia.Models;

public sealed record Media(
    Guid Id,
    string Slug,
    Guid CategoryId,
    short CategoryYear,
    string CategorySlug,
    string Type,
    bool IsFavorite,
    IEnumerable<MediaFile> Files
);

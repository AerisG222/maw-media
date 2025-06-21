using NodaTime;

namespace MawMedia.Models;

public record Media(
    Guid Id,
    string Type,
    bool IsFavorite,
    IEnumerable<MediaFile> Files
);

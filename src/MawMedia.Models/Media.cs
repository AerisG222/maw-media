using NodaTime;

namespace MawMedia.Models;

public record Media(
    Guid Id,
    string Type,
    IEnumerable<MediaFile> Files
);

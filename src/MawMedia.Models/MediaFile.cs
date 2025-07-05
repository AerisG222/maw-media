namespace MawMedia.Models;

public record MediaFile(
    Guid Id,
    string Scale,
    string Type,
    string Path
);

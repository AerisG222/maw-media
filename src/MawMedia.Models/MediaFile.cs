namespace MawMedia.Models;

public sealed record MediaFile(
    Guid Id,
    string Scale,
    string Type,
    string Path
);

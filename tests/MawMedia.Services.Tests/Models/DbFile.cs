namespace MawMedia.Services.Tests.Models;

public record DbFile(
    Guid Id,
    Guid MediaId,
    Guid TypeId,
    Guid ScaleId,
    int Width,
    int Height,
    long Bytes,
    string Path
);

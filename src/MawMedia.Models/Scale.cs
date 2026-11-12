namespace MawMedia.Models;

public sealed record Scale(
    Guid Id,
    string Code,
    int Width,
    int Height,
    bool FillsDimensions
);

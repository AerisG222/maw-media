namespace MawMedia.Models;

public record Scale(
    Guid Id,
    string Code,
    int Width,
    int Height,
    bool FillsDimensions
);

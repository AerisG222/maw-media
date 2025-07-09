namespace MawMedia.Models;

public record Scale(
    string Code,
    int Width,
    int Height,
    bool FillsDimensions
);

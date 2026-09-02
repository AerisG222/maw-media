namespace MawMedia.Services;

public class PlaceCoverConfig
{
    // the directory published covers are copied into.  it must not be, or contain,
    // the asset root: this branch skips the per file access check, so an
    // overlapping configuration would serve the private library through it.
    public required string RootDirectory { get; set; }
}

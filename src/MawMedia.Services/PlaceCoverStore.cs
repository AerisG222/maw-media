using MawMedia.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MawMedia.Services;

public class PlaceCoverStore
    : IPlaceCoverStore
{
    public const string EXTENSION = Constants.PlaceCoverExtension;

    readonly ILogger _log;
    readonly string _root;
    readonly string _assetRoot;

    public PlaceCoverStore(
        ILogger<PlaceCoverStore> log,
        IOptions<PlaceCoverConfig> config,
        IOptions<AssetConfig> assetConfig
    )
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(assetConfig);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Value.RootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetConfig.Value.RootDirectory);

        _log = log;
        _root = Path.GetFullPath(config.Value.RootDirectory);
        _assetRoot = Path.GetFullPath(assetConfig.Value.RootDirectory);

        // the cover directory must not be the asset root, nor anywhere inside it.
        // this branch skips the per file access check that governs the rest of
        // /assets, so an overlapping configuration would hand the entire library to
        // any signed in caller.  failing at startup is the only acceptable
        // response - a misconfiguration discovered at request time has already
        // served the files.
        if (Overlaps(_root, _assetRoot))
        {
            throw new InvalidOperationException(
                $"The place cover directory ('{_root}') must not overlap the asset root ('{_assetRoot}'): " +
                "covers are served without authorization.");
        }
    }

    public async Task Publish(Guid placeId, string sourceRelativePath, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRelativePath);

        // the stored path is a url - "/assets/2007/..." - and the asset root on disk
        // is what /assets resolves to, so the prefix comes off before the join.
        // TrimStart('/') alone produced <root>/assets/2007/... and a file that was
        // never there.
        var source = Path.GetFullPath(
            Path.Combine(_assetRoot, AssetPathBuilder.ToRelativeFilePath(sourceRelativePath)));

        // the path comes from the database rather than from a caller, but it is
        // still concatenated into a filesystem path, so it is checked rather than
        // trusted.  a value that escapes the asset root would read an arbitrary
        // file and then publish it past the per file access check.
        if (!source.StartsWith(_assetRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Refusing to publish a cover from outside the asset root: '{sourceRelativePath}'.");
        }

        if (!File.Exists(source))
        {
            throw new FileNotFoundException("The rendition to publish as a cover does not exist.", source);
        }

        // the directory is not created here.  it is a mount supplied by the deploy -
        // the ansible playbook makes it and the pod binds it in - so an absent one
        // is a broken deployment rather than a first run.  StaticFilesExtensions
        // already refuses to start without it, which is the right moment to find
        // out; creating it here would instead write files into a path nothing is
        // serving from.
        //
        // named for the place, so replacing a cover overwrites one file rather than
        // orphaning a previous name.  the url stays fresh through the ?v= that
        // media.place.cover_created supplies, not through the file name.
        var destination = PathFor(placeId);
        var staging = $"{destination}.{Guid.CreateVersion7()}.tmp";

        try
        {
            // copied verbatim.  the derived renditions carry only structural avif
            // metadata - no gps, no camera make or serial - which is what makes a
            // byte copy safe and a re-encode unnecessary.  the originals do carry
            // all of that, which is why media.get_place_cover_candidate never
            // offers 'src'.
            await using (var input = File.OpenRead(source))
            await using (var output = File.Create(staging))
            {
                await input.CopyToAsync(output, token);
            }

            // overwrite, so a replacement is atomic and no reader ever sees a
            // half written image
            File.Move(staging, destination, true);
        }
        catch
        {
            File.Delete(staging);

            throw;
        }

        _log.LogInformation("Published cover for place {PLACE} from {SOURCE}", placeId, sourceRelativePath);
    }

    public bool Delete(Guid placeId)
    {
        var path = PathFor(placeId);

        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);

        return true;
    }

    // the id is a Guid before it reaches here, so it cannot contain separators or
    // traversal segments - the same reasoning FaceImageStore.PathFor relies on
    string PathFor(Guid placeId) => Path.Combine(_root, $"{placeId}{EXTENSION}");

    static bool Overlaps(string a, string b)
    {
        var x = a.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var y = b.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return x.StartsWith(y, StringComparison.Ordinal) || y.StartsWith(x, StringComparison.Ordinal);
    }
}

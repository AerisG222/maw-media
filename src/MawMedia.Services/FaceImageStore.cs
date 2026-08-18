using MawMedia.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MawMedia.Services;

public class FaceImageStore
    : IFaceImageStore
{
    // avif matches what MawMediaPublisher already produces for video posters, so
    // every image this system serves shares one format, and it is markedly
    // smaller than jpeg at thumbnail sizes.  the conversion happens in
    // maw-media-ai, which already has an imaging stack open when it crops.
    //
    // a single format also keeps the path derivable from the face id alone - no
    // database lookup and no directory probe to answer "where is this image".
    // maw-media never inspects the bytes, so supporting more formats would mean
    // recording the extension somewhere.
    public const string EXTENSION = ".avif";
    public const string CONTENT_TYPE = "image/avif";

    readonly ILogger _log;
    readonly string _root;

    public FaceImageStore(
        ILogger<FaceImageStore> log,
        IOptions<FaceImageConfig> config
    )
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Value.RootDirectory);

        _log = log;
        _root = config.Value.RootDirectory;
    }

    public async Task Save(Guid faceId, Stream content, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        Directory.CreateDirectory(_root);

        var path = PathFor(faceId);

        // written to a sibling temp file and moved into place so a reader can
        // never observe a half written image, and a failed upload leaves the
        // previous one intact
        var staging = $"{path}.{Guid.CreateVersion7()}.tmp";

        try
        {
            await using (var file = File.Create(staging))
            {
                await content.CopyToAsync(file, token);
            }

            File.Move(staging, path, true);
        }
        catch
        {
            File.Delete(staging);

            throw;
        }

        _log.LogInformation("Stored face image for {FACE}", faceId);
    }

    public Stream? Open(Guid faceId)
    {
        var path = PathFor(faceId);

        return File.Exists(path)
            ? File.OpenRead(path)
            : null;
    }

    public bool Delete(Guid faceId)
    {
        var path = PathFor(faceId);

        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);

        return true;
    }

    // the id is bound as a Guid before it reaches here, so it cannot contain
    // separators or traversal segments
    string PathFor(Guid faceId) => Path.Combine(_root, $"{faceId}{EXTENSION}");
}

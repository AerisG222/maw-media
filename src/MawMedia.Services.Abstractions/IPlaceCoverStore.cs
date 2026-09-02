namespace MawMedia.Services.Abstractions;

// the published copy of a place's cover photograph.
//
// separate from IFaceImageStore despite the near identical shape, because the two
// directories are governed by different rules: a face crop is checked against the
// caller, a cover is not.  one interface over both would make it possible to write
// an image into the wrong directory by passing the wrong dependency, and nothing
// in the type system would object.
//
// keyed by place id, like face crops are keyed by face id, so the path is
// derivable from the row with no lookup and no directory probe.  a place has at
// most one cover, so there is nothing a second key would distinguish.
public interface IPlaceCoverStore
{
    // copies an existing rendition over the place's cover, replacing any previous
    // one in place.
    //
    // the source must be a path from media.get_place_cover_candidate, which yields
    // only the cover scale and never 'src' - the originals carry gps and camera
    // identity in exif, and a cover is served to every signed in caller without the
    // per file check that governs the rest of the library.
    Task Publish(Guid placeId, string sourceRelativePath, CancellationToken token = default);

    // removes a place's cover.  returns false when there is no file, which is not
    // an error: clearing a place that never had one removes nothing.
    bool Delete(Guid placeId);
}

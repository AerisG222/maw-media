namespace MawMedia.Services.Abstractions;

// stores the cropped image maw-media-ai publishes for a face, keyed by the face
// id.  the bytes are written and read verbatim - no decoding, resizing or
// transcoding happens here, which is the point: cropping stays in the project
// that already has an imaging stack.
public interface IFaceImageStore
{
    Task Save(Guid faceId, Stream content, CancellationToken token = default);
    Stream? Open(Guid faceId);
    bool Delete(Guid faceId);
}

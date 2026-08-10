namespace MawMedia.Models.FaceRecognition;

// a single detected face.
//
// FilePath is the published asset path (/assets/{year}/{dir}/{scale}/{file}).
// maw-media-ai never learns this system's media ids; the path is resolved
// against media.file when the batch is applied, so a file renamed upstream
// surfaces as an unresolved_path outcome rather than silently linking to the
// wrong media.
//
// the box is normalised 0..1 against the source image so a crop survives any
// scale.  a detector may report slightly outside that range for a face clipped
// by the frame edge.
public record FaceSync(
    Guid Id,
    string FilePath,
    Guid? PersonId,
    decimal BoxX,
    decimal BoxY,
    decimal BoxWidth,
    decimal BoxHeight,
    float DetectionScore,
    long SourceRevision
);

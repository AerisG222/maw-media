namespace MawMedia.Models.FaceRecognition;

// one detected face in a media item, for drawing an overlay.
//
// the box is normalised 0..1 against the full frame, so it applies to any scale
// the client happens to be showing.  a detector may report slightly outside that
// range for a face the frame cuts off, so clamp when drawing.
//
// PersonId is null both for a face nobody has been assigned to and for one whose
// person the caller is not allowed to know about - the two are deliberately
// indistinguishable.
public record Face(
    Guid Id,
    Guid? PersonId,
    decimal BoxX,
    decimal BoxY,
    decimal BoxWidth,
    decimal BoxHeight
);

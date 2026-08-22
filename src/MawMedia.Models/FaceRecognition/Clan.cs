using NodaTime;

namespace MawMedia.Models.FaceRecognition;

// a caller's saved selection of people, so "the kids" can be picked once rather
// than reassembled from the face grid every time.
//
// private to whoever created it.  Members carry the full Person shape - face url,
// per caller media count, favourite flag - so a client can draw a clan with its
// faces without a second call, and they are filtered by the same visibility rule
// the person list uses.  a member the caller can no longer see is simply absent,
// which means Members can be shorter than what was originally saved, and can be
// empty.
public record Clan(
    Guid Id,
    string Name,
    Instant Created,
    Instant Modified,
    IEnumerable<Person> Members
);

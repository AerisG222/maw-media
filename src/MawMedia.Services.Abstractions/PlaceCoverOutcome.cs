namespace MawMedia.Services.Abstractions;

// why a cover change succeeded or did not.
//
// an enum rather than an exception because every one of these is an ordinary
// answer a client acts on, and because the route has to map them to different
// status codes - Forbidden, NotFound and BadRequest are three different things to
// whoever is looking at an admin screen.
//
// mirrors ClanOutcome, which exists for the same reason on the clan write side.
public enum PlaceCoverOutcome
{
    Ok,

    // the caller is not an admin.  choosing a cover publishes a photograph
    // outside the authorization boundary, so it is not a thing a reader may do.
    NotAdmin,

    // no such place
    PlaceNotFound,

    // the media is not at this place or anywhere beneath it, or the caller cannot
    // see it.  the two are deliberately one answer - distinguishing them would let
    // an admin screen probe for media behind a role the caller lacks.
    MediaNotAtPlace,

    // the media has no rendition at the cover scale.  covers are always published
    // from 'qvg-fill' so every tile is the same shape, and a media whose
    // renditions were never generated - or that only ever had an original, which
    // is never publishable - cannot be a cover.
    NoPublishableRendition
}

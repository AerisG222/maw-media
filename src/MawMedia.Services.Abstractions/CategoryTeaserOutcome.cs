namespace MawMedia.Services.Abstractions;

// why setting a category's teaser succeeded or did not.
//
// an enum rather than a bool since the restriction refusal, because that one is
// not a "no such thing" - the category and media both exist and the caller may
// change them.  a client has to be able to tell it apart, or the admin screen can
// only say "not found" about a photo it is looking at.
public enum CategoryTeaserOutcome
{
    Ok,

    // the caller does not own the category, or the category does not exist, or
    // the media is not in it.  one answer, as it always was, so this cannot be
    // used to probe for categories behind a role the caller lacks.
    NotFound,

    // the media is restricted to fewer roles than the category grants.  the
    // teaser is the category's tile, shown to everyone who can see the category,
    // so a restricted media cannot be it.
    MediaRestricted
}

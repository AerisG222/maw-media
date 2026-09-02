namespace MawMedia.Models;

// a browsable location - a country, a state or a city - as end user applications
// see them.
//
// deliberately flat.  the hierarchy is expressed by ParentId alone rather than by
// nesting children inside their parent, because the browse is a drill-down: a
// client asks for one level at a time and never has a use for the whole tree in
// one response.
//
// Kind is returned rather than implied by how deep the caller drilled.  a
// country's children are usually its states, but not always - Macao and Hong Kong
// have no state level, so their cities parent straight to the country and a single
// listing can mix the two.
//
// MediaCount is scoped to what the caller may see, exactly like Person.MediaCount,
// so it cannot be used to infer how much exists behind a permission they lack.  it
// covers the place's whole subtree: a country's count includes every photo in its
// states and their cities.
//
// CoverUrl is an admin's hand picked photograph representing the place, and is
// absolute so clients do not assemble it - matching how media file urls are
// returned.  null when no cover has been chosen.
//
// it is served from /assets/covers, which requires a signed in caller holding
// media:read but performs none of the per file access checking the rest of
// /assets does.  that is the point: a cover renders for anyone browsing, even a
// caller who cannot reach the category the photograph came from.  the control
// lives in the choosing, not the serving - see media.set_place_cover.
// see docs/browse-by-location.md
public record Place(
    Guid Id,
    Guid? ParentId,
    string Kind,
    string Name,
    string Slug,
    int MediaCount,

    // the names above this place, root first and excluding itself, so a client can
    // label a result without walking parent ids.  it is what makes a search result
    // legible: this library holds two cities called Zhuhai, both under a parent
    // called Guangdong, and only the grandparent tells them apart.
    //
    // empty for a country.  an array rather than a joined string, so the client
    // picks its own separator - the same choice Category.MediaTypes makes.
    string[] AncestorNames,

    string? CoverUrl,

    // which photograph the cover was published from, or null when there is none.
    // an admin picker needs it to show the current choice as selected, which it
    // cannot do from CoverUrl - that names the published copy, not the original.
    Guid? CoverMediaId
);

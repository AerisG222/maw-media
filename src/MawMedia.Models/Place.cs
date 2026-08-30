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
// there is no teaser image field yet.  the intent is a curated image *of* the
// place rather than one drawn from the caller's own media; because it would be
// identical for every caller it can be a plain column on media.place and appear
// here as an additional property, without changing any of the ones below.
// see docs/browse-by-location.md
public record Place(
    Guid Id,
    Guid? ParentId,
    string Kind,
    string Name,
    string Slug,
    int MediaCount
);

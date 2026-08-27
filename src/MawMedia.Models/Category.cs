using NodaTime;

namespace MawMedia.Models;

public record Category(
    Guid Id,
    short Year,
    string Slug,
    string Name,
    LocalDate EffectiveDate,
    Instant Modified,
    bool IsFavorite,
    Media Teaser,
    string[] MediaTypes,

    // how many of the caller's visible media in this category a given person or
    // clan appears in.  only the person/clan category views populate it - it is
    // null everywhere else, where "the media in this category" is the whole
    // category and the client already knows it.
    int? MediaCount = null
);

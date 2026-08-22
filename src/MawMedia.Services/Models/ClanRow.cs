using NodaTime;

namespace MawMedia.Services.Models;

// the shape media.get_clans returns: one row per (clan, member), with the person
// columns null for a clan that has no members the caller can see.  the repository
// groups these back into Clan records, the same way AssembleMedia turns file rows
// back into Media.
record ClanRow(
    Guid ClanId,
    string ClanName,
    Instant Created,
    Instant Modified,
    Guid? PersonId,
    string? PersonName,
    string? PersonSlug,
    Guid? PreferredFaceId,
    int? MediaCount,
    bool? IsFavorite
);

namespace MawMedia.Services.Models;

// media.create_clan returns a row rather than a scalar so a rejection can say
// which rule it broke - a duplicate name and an unusable person id need
// different answers at the http boundary
record CreateClanRow(
    Guid? ClanId,
    int Result
);

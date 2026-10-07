namespace MawMedia.Services.Models;

// one row of media.set_media_roles: a problem with the request, or an 'applied'
// row per media when it succeeded.  see that function for the outcomes.
class MediaRoleOutcomeRow
{
    public Guid? AffectedMediaId { get; set; }
    public required string Outcome { get; set; }
    public string? Detail { get; set; }
}

// the result of media.get_media_roles
class MediaRolesRow
{
    public int Result { get; set; }
    public string[]? RoleNames { get; set; }
}

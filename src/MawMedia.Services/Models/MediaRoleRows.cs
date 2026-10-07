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

// one row of media.get_restricted_media
class RestrictedMediaRow
{
    public Guid MediaId { get; set; }
    public required string MediaSlug { get; set; }
    public required string MediaType { get; set; }
    public Guid CategoryId { get; set; }
    public required string CategoryName { get; set; }
    public short CategoryYear { get; set; }
    public required string CategorySlug { get; set; }
    public required string[] Roles { get; set; }
    public bool IsVisibleToYou { get; set; }
}

// one row of media.set_category_roles; the same shape as media.set_media_roles
class CategoryRoleOutcomeRow
{
    public Guid? AffectedMediaId { get; set; }
    public required string Outcome { get; set; }
    public string? Detail { get; set; }
}

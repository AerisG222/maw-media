using System.Security.Claims;
using MawMedia.Authorization.Claims;
using MawMedia.Services.Abstractions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MawMedia.Routes;

// the roles a media can be restricted to, for the admin screen's picker.
//
// names, because that is how a restriction is set: PUT /media/{id}/roles takes
// {"roles": ["admin"]}.  admin only, which the database enforces, and under the
// write scope because nothing but restriction editing needs it.
public static class RoleRoutes
{
    public static RouteGroupBuilder MapRoleRoutes(this RouteGroupBuilder group)
    {
        group
            .MapGet("/", GetRoles)
            .WithName("roles")
            .WithSummary("Roles")
            .WithDescription("The names of every role a media can be restricted to")
            .RequireAuthorization(AuthorizationPolicies.MediaWriter);

        return group;
    }

    static async Task<Results<Ok<IReadOnlyList<string>>, NotFound, ForbidHttpResult>> GetRoles(
        IMediaRepository repo,
        ClaimsPrincipal user,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var roles = await repo.GetRoles(userId.Value, token);

        return roles == null
            ? TypedResults.Forbid()
            : TypedResults.Ok(roles);
    }
}

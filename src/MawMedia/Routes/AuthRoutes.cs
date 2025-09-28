using System.Security.Claims;
using MawMedia.Authorization.Claims;
using MawMedia.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MawMedia.Routes;

public static class AuthRoutes
{
    public static RouteGroupBuilder MapAuthRoutes(this RouteGroupBuilder group)
    {
        group
            .MapGet("/is-admin", GetIsAdmin)
            .WithName("auth-is-admin")
            .WithSummary("Is Admin")
            .WithDescription("Identifies if user is an admin")
            .RequireAuthorization(AuthorizationPolicies.User);

        return group;
    }

    static async Task<Results<Ok<bool>, ForbidHttpResult>> GetIsAdmin(
        IAuthRepository repo,
        ClaimsPrincipal user
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await repo.GetIsAdmin(userId.Value))
            : TypedResults.Ok(false);
    }
}

using System.Security.Claims;
using MawMedia.Authorization.Claims;
using MawMedia.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MawMedia.Routes;

public static class AuthRoutes
{
    public static RouteGroupBuilder MapAuthRoutes(this RouteGroupBuilder group)
    {
        group
            .MapGet("/account-status", GetAccountState)
            .WithName("auth-account-status")
            .WithSummary("Account Status")
            .WithDescription("Identifies if user is activated and if they are an admin")
            .RequireAuthorization(AuthorizationPolicies.User);

        return group;
    }

    static Results<Ok<UserStatus>, ForbidHttpResult> GetAccountState(
        ClaimsPrincipal user
    )
    {
        return TypedResults.Ok(new UserStatus(
            user.GetUserStatus(),
            user.GetIsAdmin()
        ));
    }
}

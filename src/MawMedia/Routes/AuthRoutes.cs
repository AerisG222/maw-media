using System.Security.Claims;
using MawMedia.Authorization.Claims;
using MawMedia.Extensions;
using MawMedia.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

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

        group
            .MapPost("/asset-session", CreateAssetSession)
            .WithName("auth-create-asset-session")
            .WithSummary("Create Asset Session")
            .WithDescription("Sets the cookie a browser uses to load media files, which can not carry a bearer token")
            .RequireAuthorization(AuthorizationPolicies.User);

        // anonymous so a browser can still clear its cookie once its token is gone.
        // cors refuses the preflight from any other origin, and all it can do is
        // remove the caller's own cookie.
        group
            .MapDelete("/asset-session", DeleteAssetSession)
            .WithName("auth-delete-asset-session")
            .WithSummary("Delete Asset Session")
            .WithDescription("Clears the cookie set by creating an asset session")
            .AllowAnonymous();

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

    static async Task<Ok<AssetSession>> CreateAssetSession(
        HttpContext ctx,
        ClaimsPrincipal user,
        IOptions<OAuthConfig> oauth
    )
    {
        var lifetime = await ctx.SignInToAssetSession(user, oauth.Value);

        return TypedResults.Ok(new AssetSession((int)lifetime.TotalSeconds));
    }

    static async Task DeleteAssetSession(HttpContext ctx)
    {
        await ctx.SignOutOfAssetSession();

        ctx.Response.StatusCode = StatusCodes.Status204NoContent;
    }
}

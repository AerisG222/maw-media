using MawMedia.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MawMedia.Routes;

public static class AuthRoutes
{
    static readonly Guid DUMMYUSER = Guid.Parse("01997368-32db-7af5-83c3-00712e2304fd");

    public static RouteGroupBuilder MapAuthRoutes(this RouteGroupBuilder group)
    {
        group
            .MapGet("/is-admin", GetIsAdmin)
            .WithName("config-is-admin")
            .WithSummary("Is Admin")
            .WithDescription("Identifies if user is an admin")
            .RequireAuthorization(AuthorizationPolicies.User);

        return group;
    }

    static async Task<Results<Ok<bool>, ForbidHttpResult>> GetIsAdmin(IAuthRepository repo) =>
        TypedResults.Ok(await repo.GetIsAdmin(DUMMYUSER));
}

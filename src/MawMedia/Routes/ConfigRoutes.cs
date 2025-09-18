using MawMedia.Models;
using MawMedia.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MawMedia.Routes;

public static class ConfigRoutes
{
    static readonly Guid DUMMYUSER = Guid.Parse("0197e02e-7c7f-7c1e-bb77-ee35921e4c51");

    public static RouteGroupBuilder MapConfigRoutes(this RouteGroupBuilder group)
    {
        group
            .MapGet("/is-admin", GetIsAdmin)
            .WithName("config-is-admin")
            .WithSummary("Is Admin")
            .WithDescription("Identifies if user is an admin")
            .RequireAuthorization(AuthorizationPolicies.User);

        group
            .MapGet("/scales", GetScales)
            .WithName("config-scales")
            .WithSummary("Scales")
            .WithDescription("Lists available scales")
            .RequireAuthorization(AuthorizationPolicies.User);

        return group;
    }

    static async Task<Results<Ok<IEnumerable<Scale>>, ForbidHttpResult>> GetScales(IConfigRepository repo) =>
        TypedResults.Ok(await repo.GetScales());

    static async Task<Results<Ok<bool>, ForbidHttpResult>> GetIsAdmin(IConfigRepository repo) =>
        TypedResults.Ok(await repo.GetIsAdmin(DUMMYUSER));
}

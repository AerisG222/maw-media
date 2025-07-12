using Microsoft.AspNetCore.Http.HttpResults;
using MawMedia.Models;
using MawMedia.Services;

namespace MawMedia.Routes;

public static class ConfigRoutes
{
    public static RouteGroupBuilder MapConfigRoutes(this RouteGroupBuilder group)
    {
        group
            .MapGet("/scales", GetScales)
            .WithName("config-scales")
            .WithSummary("Scales")
            .WithDescription("Lists available scales")
            .RequireAuthorization(AuthPolicies.User);

        return group;
    }

    static async Task<Results<Ok<IEnumerable<Scale>>, ForbidHttpResult>> GetScales(IConfigRepository repo) =>
        TypedResults.Ok(await repo.GetScales());
}

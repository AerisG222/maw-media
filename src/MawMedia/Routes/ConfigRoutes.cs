using MawMedia.Models;
using MawMedia.Services.Abstractions;
using Microsoft.AspNetCore.Http.HttpResults;

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
            .RequireAuthorization(AuthorizationPolicies.User);

        return group;
    }

    static async Task<Results<Ok<IEnumerable<Scale>>, ForbidHttpResult>> GetScales(IConfigRepository repo) =>
        TypedResults.Ok(await repo.GetScales());
}

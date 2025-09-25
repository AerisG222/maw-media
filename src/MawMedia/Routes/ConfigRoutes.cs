using MawMedia.Models;
using MawMedia.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MawMedia.Routes;

public static class ConfigRoutes
{
    static readonly Guid DUMMYUSER = Guid.Parse("01997368-32db-7af5-83c3-00712e2304fd");

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

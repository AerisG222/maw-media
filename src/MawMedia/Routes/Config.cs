using Microsoft.AspNetCore.Http.HttpResults;
using MawMedia.Models;
using MawMedia.Services;

namespace MawMedia.Routes;

public static class ConfigRoutes
{
    static readonly Guid DUMMYUSER = Guid.Parse("0197e02e-7c7f-7c1e-bb77-ee35921e4c51");

    public static RouteGroupBuilder MapConfigRoutes(this RouteGroupBuilder group)
    {
        // TODO: consider removing to force calling by year to effectively implement paging, or add some logic for paging/limiting results
        group
            .MapGet("/scales", GetScales)
            .WithName("config-scales")
            .WithSummary("Scales")
            .WithDescription("Lists available scales");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        return group;
    }

    static async Task<Results<Ok<IEnumerable<Scale>>, ForbidHttpResult>> GetScales(IConfigRepository repo, HttpRequest request) =>
        TypedResults.Ok(await repo.GetScales());
}

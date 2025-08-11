using MawMedia.Models;
using MawMedia.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace MawMedia.Routes;

public static class StatRoutes
{
    static readonly Guid DUMMYUSER = Guid.Parse("0197e02e-7c7f-7c1e-bb77-ee35921e4c51");

    public static RouteGroupBuilder MapStatRoutes(this RouteGroupBuilder group)
    {
        group
            .MapGet("/", GetStats)
            .WithName("stats-overall")
            .WithSummary("Overall Stats")
            .WithDescription("Get overall stats by year")
            .RequireAuthorization(AuthorizationPolicies.StatsReader);

        group
            .MapGet("/{year}", GetStatsForYear)
            .WithName("stats-for-year")
            .WithSummary("Stats for Year")
            .WithDescription("Get category stats for year")
            .RequireAuthorization(AuthorizationPolicies.StatsReader);

        return group;
    }

    static async Task<Results<Ok<IEnumerable<YearStat>>, ForbidHttpResult>> GetStats(IStatRepository repo) =>
        TypedResults.Ok(await repo.GetStats(DUMMYUSER));

    static async Task<Results<Ok<IEnumerable<CategoryStat>>, ForbidHttpResult>> GetStatsForYear(IStatRepository repo, [FromRoute] short year) =>
        TypedResults.Ok(await repo.GetStatsForYear(DUMMYUSER, year));
}

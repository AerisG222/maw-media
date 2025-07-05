using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MawMedia.Models;
using MawMedia.Services;

namespace MawMedia.Routes;

public static class StatRoutes
{
    static readonly Guid DUMMYUSER = Guid.Parse("01977b3a-6db0-7384-87ad-8e56aad783ef");

    public static RouteGroupBuilder MapStatRoutes(this RouteGroupBuilder group)
    {
        group
                .MapGet("/", GetStats)
                .WithName("stats-overall")
                .WithSummary("Overall Stats")
                .WithDescription("Get overall stats by year");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        group
            .MapGet("/{year}", GetStatsForYear)
                .WithName("stats-for-year")
                .WithSummary("Stats for Year")
                .WithDescription("Get category stats for year");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        return group;
    }

    static async Task<Results<Ok<IEnumerable<YearStat>>, ForbidHttpResult>> GetStats(IStatRepository repo) =>
        TypedResults.Ok(await repo.GetStats(DUMMYUSER));

    static async Task<Results<Ok<IEnumerable<CategoryStat>>, ForbidHttpResult>> GetStatsForYear(IStatRepository repo, [FromRoute] short year) =>
        TypedResults.Ok(await repo.GetStatsForYear(DUMMYUSER, year));
}

using System.Security.Claims;
using MawMedia.Authorization.Claims;
using MawMedia.Models;
using MawMedia.Services.Abstractions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace MawMedia.Routes;

public static class StatRoutes
{
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

    static async Task<Results<Ok<IEnumerable<YearStat>>, ForbidHttpResult>> GetStats(
        IStatRepository repo,
        ClaimsPrincipal user
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await repo.GetStats(userId.Value))
            : TypedResults.Ok(Array.Empty<YearStat>().AsEnumerable());
    }

    static async Task<Results<Ok<IEnumerable<CategoryStat>>, ForbidHttpResult>> GetStatsForYear(
        IStatRepository repo,
        ClaimsPrincipal user,
        [FromRoute] short year
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await repo.GetStatsForYear(userId.Value, year))
            : TypedResults.Ok(Array.Empty<CategoryStat>().AsEnumerable());
    }
}

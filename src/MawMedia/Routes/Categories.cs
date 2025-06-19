using MawMedia.Models;
using MawMedia.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MawMedia.Routes;

public static class Categories
{
    static readonly Guid DUMMYUSER = Guid.Parse("01977b3a-6db0-7384-87ad-8e56aad783ef");

    public static RouteGroupBuilder MapCategoryRoutes(this RouteGroupBuilder group)
    {
        group
            .MapGet("/", GetCategories)
            .WithName("categories")
            .WithSummary("Categories")
            .WithDescription("Lists categories");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        return group;
    }

    static async Task<Results<Ok<IEnumerable<Category>>, ForbidHttpResult>> GetCategories(ICategoryRepository repo, HttpRequest request) =>
        TypedResults.Ok(await repo.GetCategories(DUMMYUSER));
}

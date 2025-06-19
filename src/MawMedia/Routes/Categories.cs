using MawMedia.Models;
using MawMedia.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

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

        group
            .MapGet("/{id}", GetCategory)
            .WithName("category")
            .WithSummary("Category")
            .WithDescription("Get single category");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        group
            .MapGet("/{id}/media", GetCategoryMedia)
            .WithName("category-media")
            .WithSummary("Category Media")
            .WithDescription("Get media for single category");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        return group;
    }

    static async Task<Results<Ok<IEnumerable<Category>>, ForbidHttpResult>> GetCategories(ICategoryRepository repo, HttpRequest request) =>
        TypedResults.Ok(await repo.GetCategories(DUMMYUSER));

    static async Task<Results<Ok<Category>, ForbidHttpResult>> GetCategory(ICategoryRepository repo, [FromRoute] Guid id) =>
        TypedResults.Ok(await repo.GetCategory(DUMMYUSER, id));

    static async Task<Results<Ok<IEnumerable<Models.Media>>, ForbidHttpResult>> GetCategoryMedia(ICategoryRepository repo, [FromRoute] Guid id) =>
        TypedResults.Ok(await repo.GetCategoryMedia(DUMMYUSER, id));
}

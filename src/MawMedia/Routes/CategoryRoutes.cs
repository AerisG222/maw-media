using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using MawMedia.Models;
using MawMedia.Services;
using MawMedia.ViewModels;

namespace MawMedia.Routes;

public static class CategoryRoutes
{
    static readonly Guid DUMMYUSER = Guid.Parse("01977b3a-6db0-7384-87ad-8e56aad783ef");

    public static RouteGroupBuilder MapCategoryRoutes(this RouteGroupBuilder group)
    {
        // TODO: consider removing to force calling by year to effectively implement paging, or add some logic for paging/limiting results
        group
            .MapGet("/", GetCategories)
            .WithName("categories")
            .WithSummary("Categories")
            .WithDescription("Lists categories");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        group
            .MapGet("/years", GetCategoryYears)
            .WithName("category-years")
            .WithSummary("Category Years")
            .WithDescription("Lists category years");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        group
            .MapGet("/years/{year}", GetCategoriesForYear)
            .WithName("categories-for-years")
            .WithSummary("Categories for Year")
            .WithDescription("Lists categories for a specific year");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        group
            .MapGet("/updates/{date}", GetCategoryUpdates)
            .WithName("category-updates")
            .WithSummary("Category Updates")
            .WithDescription("Get category updates after a specified date/time");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        group
            .MapGet("/{id}", GetCategory)
            .WithName("category")
            .WithSummary("Category")
            .WithDescription("Get single category");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        group
            .MapPost("/{id}/favorite", FavoriteCategory)
            .WithName("category-favorite")
            .WithSummary("Favorite Category")
            .WithDescription("Favorite specified category");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        group
            .MapPost("/{id}/teaser", SetCategoryTeaser)
            .WithName("category-set-teaser")
            .WithSummary("Set Category Teaser")
            .WithDescription("Set category teaser");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        group
            .MapGet("/{id}/media", GetCategoryMedia)
            .WithName("category-media")
            .WithSummary("Category Media")
            .WithDescription("Get media for single category");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        return group;
    }

    static async Task<Results<Ok<IEnumerable<short>>, ForbidHttpResult>> GetCategoryYears(ICategoryRepository repo, HttpRequest request) =>
        TypedResults.Ok(await repo.GetCategoryYears(DUMMYUSER));

    static async Task<Results<Ok<IEnumerable<Category>>, ForbidHttpResult>> GetCategories(ICategoryRepository repo, HttpRequest request) =>
        TypedResults.Ok(await repo.GetCategories(DUMMYUSER));

    static async Task<Results<Ok<IEnumerable<Category>>, ForbidHttpResult>> GetCategoriesForYear(ICategoryRepository repo, HttpRequest request, [FromRoute] short year) =>
        TypedResults.Ok(await repo.GetCategories(DUMMYUSER, year));

    static async Task<Results<Ok<IEnumerable<Category>>, ForbidHttpResult>> GetCategoryUpdates(ICategoryRepository repo, HttpRequest request, DateTime date) =>
        TypedResults.Ok(await repo.GetCategoryUpdates(DUMMYUSER, Instant.FromDateTimeUtc(date.ToUniversalTime())));

    static async Task<Results<Ok<Category>, NotFound, ForbidHttpResult>> GetCategory(ICategoryRepository repo, [FromRoute] Guid id) {
        var category = await repo.GetCategory(DUMMYUSER, id);

        if (category == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(category);
    }

    static async Task<Results<Ok<Category>, NotFound, ForbidHttpResult>> FavoriteCategory(ICategoryRepository repo, [FromRoute] Guid id, [FromBody] FavoriteRequest request)
    {
        var category = await repo.SetIsFavorite(DUMMYUSER, id, request.IsFavorite);

        if (category == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(category);
    }

    static async Task<Results<Ok<Category>, NotFound, ForbidHttpResult>> SetCategoryTeaser(ICategoryRepository repo, [FromRoute] Guid id, [FromBody] CategoryTeaserRequest request)
    {
        var category = await repo.SetTeaserMedia(DUMMYUSER, id, request.MediaId);

        if (category == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(category);
    }

    static async Task<Results<Ok<IEnumerable<Media>>, ForbidHttpResult>> GetCategoryMedia(ICategoryRepository repo, [FromRoute] Guid id) =>
        TypedResults.Ok(await repo.GetCategoryMedia(DUMMYUSER, id));
}

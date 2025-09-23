using MawMedia.Models;
using MawMedia.Routes.Extensions;
using MawMedia.Services;
using MawMedia.ViewModels;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NodaTime;

namespace MawMedia.Routes;

public static class CategoryRoutes
{
    const int SEARCH_LIMIT = 24;
    const string SCALE_FULL = "full";
    static readonly Guid DUMMYUSER = Guid.Parse("01997368-32db-7af5-83c3-00712e2304fd");

    public static RouteGroupBuilder MapCategoryRoutes(this RouteGroupBuilder group)
    {
        // TODO: consider removing to force calling by year to effectively implement paging, or add some logic for paging/limiting results
        group
            .MapGet("/", GetCategories)
            .WithName("categories")
            .WithSummary("Categories")
            .WithDescription("Lists categories")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        group
            .MapGet("/years", GetCategoryYears)
            .WithName("category-years")
            .WithSummary("Category Years")
            .WithDescription("Lists category years")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        group
            .MapGet("/years/{year}", GetCategoriesForYear)
            .WithName("categories-for-years")
            .WithSummary("Categories for Year")
            .WithDescription("Lists categories for a specific year")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        group
            .MapGet("/years/{year}/no-gps", GetCategoriesWithoutGps)
            .WithName("categories-without-gps")
            .WithSummary("Categories Without GPS")
            .WithDescription("List Categories that are not tagged with a GPS position")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        group
            .MapGet("/updates/{date}", GetCategoryUpdates)
            .WithName("category-updates")
            .WithSummary("Category Updates")
            .WithDescription("Get category updates after a specified date/time")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        group
            .MapGet("/search", SearchCategories)
            .WithName("category-search")
            .WithSummary("Category Search")
            .WithDescription("Search for categories")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        group
            .MapGet("/{id}", GetCategory)
            .WithName("category")
            .WithSummary("Category")
            .WithDescription("Get single category")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        // todo: consider replacing below 2 routes w/ a patch to /{id}
        group
            .MapPost("/{id}/favorite", FavoriteCategory)
            .WithName("category-favorite")
            .WithSummary("Favorite Category")
            .WithDescription("Favorite specified category")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        group
            .MapPost("/{id}/teaser", SetCategoryTeaser)
            .WithName("category-set-teaser")
            .WithSummary("Set Category Teaser")
            .WithDescription("Set category teaser")
            .RequireAuthorization(AuthorizationPolicies.MediaWriter);

        group
            .MapGet("/{id}/media", GetCategoryMedia)
            .WithName("category-media")
            .WithSummary("Category Media")
            .WithDescription("Get media for single category")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        group
            .MapGet("/{id}/gps", GetCategoryMediaGps)
            .WithName("category-media-gps")
            .WithSummary("Category Media GPS")
            .WithDescription("Get GPS for media in a specific category")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        group
            .MapGet("/{id}/download", DownloadCategoryMedia)
            .WithName("category-download-media")
            .WithSummary("Download Category Media to Zip file")
            .WithDescription("Download full set of high resolution images for category")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        return group;
    }

    static async Task<Results<Ok<IEnumerable<short>>, ForbidHttpResult>> GetCategoryYears(ICategoryRepository repo) =>
        TypedResults.Ok(await repo.GetCategoryYears(DUMMYUSER));

    static async Task<Results<Ok<IEnumerable<Category>>, ForbidHttpResult>> GetCategories(ICategoryRepository repo, HttpRequest request) =>
        TypedResults.Ok(await repo.GetCategories(DUMMYUSER, request.GetBaseUrl()));

    static async Task<Results<Ok<IEnumerable<Category>>, ForbidHttpResult>> GetCategoriesForYear(ICategoryRepository repo, HttpRequest request, [FromRoute] short year) =>
        TypedResults.Ok(await repo.GetCategories(DUMMYUSER, request.GetBaseUrl(), year));

    static async Task<Results<Ok<IEnumerable<Category>>, ForbidHttpResult>> GetCategoryUpdates(ICategoryRepository repo, HttpRequest request, DateTime date) =>
        TypedResults.Ok(await repo.GetCategoryUpdates(DUMMYUSER, Instant.FromDateTimeUtc(date.ToUniversalTime()), request.GetBaseUrl()));

    static async Task<Results<Ok<SearchResult<Category>>, BadRequest<string>, ForbidHttpResult>> SearchCategories(ICategoryRepository repo, HttpRequest request, [FromQuery] string s, [FromQuery] int o = 0)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return TypedResults.BadRequest("Search term cannot be empty.");
        }

        return TypedResults.Ok(await repo.Search(DUMMYUSER, request.GetBaseUrl(), s, o, SEARCH_LIMIT));
    }

    static async Task<Results<Ok<IEnumerable<Guid>>, BadRequest<string>, ForbidHttpResult>> GetCategoriesWithoutGps(ICategoryRepository repo, HttpRequest request, [FromRoute] short year) =>
        TypedResults.Ok(await repo.GetCategoriesWithoutGps(DUMMYUSER, year));

    static async Task<Results<Ok<Category>, NotFound, ForbidHttpResult>> GetCategory(ICategoryRepository repo, HttpRequest request, [FromRoute] Guid id)
    {
        var category = await repo.GetCategory(DUMMYUSER, id, request.GetBaseUrl());

        if (category == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(category);
    }

    static async Task<Results<Ok<Category>, NotFound, ForbidHttpResult>> FavoriteCategory(ICategoryRepository repo, HttpRequest request, [FromRoute] Guid id, [FromBody] FavoriteRequest favRequest)
    {
        var success = await repo.SetIsFavorite(DUMMYUSER, id, favRequest.IsFavorite);

        if (success)
        {
            var category = await repo.GetCategory(DUMMYUSER, id, request.GetBaseUrl());

            return TypedResults.Ok(category);
        }

        return TypedResults.NotFound();
    }

    static async Task<Results<Ok<Category>, NotFound, ForbidHttpResult>> SetCategoryTeaser(ICategoryRepository repo, HttpRequest request, [FromRoute] Guid id, [FromBody] CategoryTeaserRequest teaserRequest)
    {
        var success = await repo.SetTeaserMedia(DUMMYUSER, id, teaserRequest.MediaId);

        if (success)
        {
            var category = await repo.GetCategory(DUMMYUSER, id, request.GetBaseUrl());

            return TypedResults.Ok(category);
        }

        return TypedResults.NotFound();
    }

    static async Task<Results<Ok<IEnumerable<Media>>, ForbidHttpResult>> GetCategoryMedia(ICategoryRepository repo, HttpRequest request, [FromRoute] Guid id) =>
        TypedResults.Ok(await repo.GetCategoryMedia(DUMMYUSER, request.GetBaseUrl(), id));

    static async Task<Results<Ok<IEnumerable<Gps>>, ForbidHttpResult>> GetCategoryMediaGps(ICategoryRepository repo, [FromRoute] Guid id) =>
        TypedResults.Ok(await repo.GetCategoryMediaGps(DUMMYUSER, id));

    static async Task<IResult> DownloadCategoryMedia(ICategoryRepository repo, IZipFileWriter zipWriter, [FromRoute] Guid id, HttpResponse response)
    {
        var filename = $"{DUMMYUSER}-{id}.zip";
        var zipFile =
            await zipWriter.GetZipFileIfExists(filename)
            ??
            await CreateCategoryDownloadZipFile(repo, zipWriter, DUMMYUSER, id, filename);

        if (zipFile == null || !zipFile.Exists)
        {
            return TypedResults.NotFound();
        }

        return Results.File(
                zipFile.FullName,
                System.Net.Mime.MediaTypeNames.Application.Zip,
                "maw-media-files.zip"
            );
    }

    static async Task<FileInfo?> CreateCategoryDownloadZipFile(ICategoryRepository repo, IZipFileWriter zipWriter, Guid userId, Guid categoryId, string filename)
    {
        var media = await repo.GetCategoryMedia(userId, string.Empty, categoryId);

        if (media == null || !media.Any())
        {
            return null;
        }

        var filesToInclude = media
            .SelectMany(m => m.Files)
            .Where(f => string.Equals(SCALE_FULL, f.Scale, StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Path)
            .ToArray();

        if (filesToInclude.Length == 0)
        {
            return null;
        }

        return await zipWriter.WriteZipFile(filename, filesToInclude);
    }
}

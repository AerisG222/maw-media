using System.Security.Claims;
using MawMedia.Authorization.Claims;
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

    static async Task<Results<Ok<IEnumerable<short>>, ForbidHttpResult>> GetCategoryYears(
        ICategoryRepository repo,
        ClaimsPrincipal user
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await repo.GetCategoryYears(userId.Value))
            : TypedResults.Ok(Array.Empty<short>().AsEnumerable());
    }

    static async Task<Results<Ok<IEnumerable<Category>>, ForbidHttpResult>> GetCategories(
        ICategoryRepository repo,
        ClaimsPrincipal user,
        HttpRequest request
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await repo.GetCategories(userId.Value, request.GetBaseUrl()))
            : TypedResults.Ok(Array.Empty<Category>().AsEnumerable());
    }

    static async Task<Results<Ok<IEnumerable<Category>>, ForbidHttpResult>> GetCategoriesForYear(
        ICategoryRepository repo,
        ClaimsPrincipal user,
        HttpRequest request,
        [FromRoute] short year
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await repo.GetCategories(userId.Value, request.GetBaseUrl(), year))
            : TypedResults.Ok(Array.Empty<Category>().AsEnumerable());
    }

    static async Task<Results<Ok<IEnumerable<Category>>, ForbidHttpResult>> GetCategoryUpdates(
        ICategoryRepository repo,
        ClaimsPrincipal user,
        HttpRequest request,
        [FromRoute] DateTime date
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await repo.GetCategoryUpdates(userId.Value, Instant.FromDateTimeUtc(date.ToUniversalTime()), request.GetBaseUrl()))
            : TypedResults.Ok(Array.Empty<Category>().AsEnumerable());
    }

    static async Task<Results<Ok<SearchResult<Category>>, BadRequest<string>, ForbidHttpResult>> SearchCategories(
        ICategoryRepository repo,
        ClaimsPrincipal user,
        HttpRequest request,
        [FromQuery] string s,
        [FromQuery] int o = 0
    )
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return TypedResults.BadRequest("Search term cannot be empty.");
        }

        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await repo.Search(userId.Value, request.GetBaseUrl(), s, o, SEARCH_LIMIT))
            : TypedResults.Ok(new SearchResult<Category>([], false, 0));
    }

    static async Task<Results<Ok<IEnumerable<Guid>>, BadRequest<string>, ForbidHttpResult>> GetCategoriesWithoutGps(
        ICategoryRepository repo,
        ClaimsPrincipal user,
        HttpRequest request,
        [FromRoute] short year
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await repo.GetCategoriesWithoutGps(userId.Value, year))
            : TypedResults.Ok(Array.Empty<Guid>().AsEnumerable());
    }

    static async Task<Results<Ok<Category>, NotFound, ForbidHttpResult>> GetCategory(
        ICategoryRepository repo,
        ClaimsPrincipal user,
        HttpRequest request,
        [FromRoute] Guid id
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var category = await repo.GetCategory(userId.Value, id, request.GetBaseUrl());

        return category != null
            ? TypedResults.Ok(category)
            : TypedResults.NotFound();
    }

    static async Task<Results<Ok<Category>, NotFound, ForbidHttpResult>> FavoriteCategory(
        ICategoryRepository repo,
        ClaimsPrincipal user,
        HttpRequest request,
        [FromRoute] Guid id,
        [FromBody] FavoriteRequest favRequest
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var success = await repo.SetIsFavorite(userId.Value, id, favRequest.IsFavorite);

        return success
            ? TypedResults.Ok(await repo.GetCategory(userId.Value, id, request.GetBaseUrl()))
            : TypedResults.NotFound();
    }

    static async Task<Results<Ok<Category>, NotFound, ForbidHttpResult>> SetCategoryTeaser(
        ICategoryRepository repo,
        ClaimsPrincipal user,
        HttpRequest request,
        [FromRoute] Guid id,
        [FromBody] CategoryTeaserRequest teaserRequest
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var success = await repo.SetTeaserMedia(userId.Value, id, teaserRequest.MediaId);

        return success
            ? TypedResults.Ok(await repo.GetCategory(userId.Value, id, request.GetBaseUrl()))
            : TypedResults.NotFound();
    }

    static async Task<Results<Ok<IEnumerable<Media>>, ForbidHttpResult>> GetCategoryMedia(
        ICategoryRepository repo,
        ClaimsPrincipal user,
        HttpRequest request,
        [FromRoute] Guid id
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await repo.GetCategoryMedia(userId.Value, request.GetBaseUrl(), id))
            : TypedResults.Ok(Array.Empty<Media>().AsEnumerable());
    }

    static async Task<Results<Ok<IEnumerable<Gps>>, ForbidHttpResult>> GetCategoryMediaGps(
        ICategoryRepository repo,
        ClaimsPrincipal user,
        [FromRoute] Guid id
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await repo.GetCategoryMediaGps(userId.Value, id))
            : TypedResults.Ok(Array.Empty<Gps>().AsEnumerable());
    }

    static async Task<IResult> DownloadCategoryMedia(
        ICategoryRepository repo,
        IZipFileWriter zipWriter,
        ClaimsPrincipal user,
        HttpResponse response,
        [FromRoute] Guid id
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var filename = $"{userId}-{id}.zip";
        var zipFile =
            await zipWriter.GetZipFileIfExists(filename)
            ??
            await CreateCategoryDownloadZipFile(repo, zipWriter, userId.Value, id, filename);

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

    static async Task<FileInfo?> CreateCategoryDownloadZipFile(
        ICategoryRepository repo,
        IZipFileWriter zipWriter,
        Guid userId,
        Guid categoryId,
        string filename
    )
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

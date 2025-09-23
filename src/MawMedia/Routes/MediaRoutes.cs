using MawMedia.Models;
using MawMedia.Routes.Extensions;
using MawMedia.Services;
using MawMedia.ViewModels;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace MawMedia.Routes;

public static class MediaRoutes
{
    static readonly Guid DUMMYUSER = Guid.Parse("01997368-32db-7af5-83c3-00712e2304fd");

    public static RouteGroupBuilder MapMediaRoutes(this RouteGroupBuilder group)
    {
        group
            .MapGet("/random/{count}", GetRandomMedia)
            .WithName("random-media")
            .WithSummary("Random Media")
            .WithDescription("Lists random media")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        group
            .MapGet("/{id}", GetMedia)
            .WithName("media")
            .WithSummary("Get Media")
            .WithDescription("Get media")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        group
            .MapGet("/{id}/metadata", GetMetadata)
            .WithName("media-metadata")
            .WithSummary("Get Media Metadata")
            .WithDescription("Get media metadata")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        group
            .MapGet("/{id}/gps", GetGps)
            .WithName("media-gps")
            .WithSummary("Get GPS for Media")
            .WithDescription("Get GPS for media")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        group
            .MapPost("/{id}/favorite", FavoriteMedia)
            .WithName("favorite-media")
            .WithSummary("Favorite Media")
            .WithDescription("Favorites media")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        group
            .MapGet("/{id}/comments", GetComments)
            .WithName("media-comments")
            .WithSummary("Get Media Comments")
            .WithDescription("Get media comments")
            .RequireAuthorization(AuthorizationPolicies.CommentReader);

        group
            .MapPost("/{id}/comments", AddComment)
            .WithName("add-media-comment")
            .WithSummary("Add Media Comment")
            .WithDescription("Add comment for media")
            .RequireAuthorization(AuthorizationPolicies.CommentWriter);

        group
            .MapPost("/{id}/gps", SetGpsOverride)
            .WithName("set-media-gps-override")
            .WithSummary("Set GPS Override for Media")
            .WithDescription("Set the GPS override for this media")
            .RequireAuthorization(AuthorizationPolicies.MediaWriter);

        group
            .MapPost("/bulk-gps-override", BulkGpsOverride)
            .WithName("bulk-set-gps-override")
            .WithSummary("Bulk GPS Override")
            .WithDescription("Set the GPS override for many media items at once")
            .RequireAuthorization(AuthorizationPolicies.MediaWriter);

        return group;
    }

    static async Task<Results<Ok<IEnumerable<Media>>, ForbidHttpResult>> GetRandomMedia(IMediaRepository repo, HttpRequest request, [FromRoute] byte count) =>
        TypedResults.Ok(await repo.GetRandomMedia(DUMMYUSER, request.GetBaseUrl(), count));

    static async Task<Results<Ok<Media>, NotFound, ForbidHttpResult>> GetMedia(IMediaRepository repo, HttpRequest request, [FromRoute] Guid id)
    {
        var media = await repo.GetMedia(DUMMYUSER, request.GetBaseUrl(), id);

        if (media == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(media);
    }

    static async Task<Results<Ok<Gps>, NotFound, ForbidHttpResult>> GetGps(IMediaRepository repo, [FromRoute] Guid id)
    {
        var gps = await repo.GetGps(DUMMYUSER, id);

        if (gps == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(gps);
    }

    static async Task<IResult> GetMetadata(IMediaRepository repo, HttpRequest request, [FromRoute] Guid id)
    {
        var metadata = await repo.GetMetadata(DUMMYUSER, id);

        if (metadata == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(metadata);
    }

    static async Task<Results<Ok<Media>, NotFound, ForbidHttpResult>> FavoriteMedia(IMediaRepository repo, HttpRequest httpRequest, [FromRoute] Guid id, [FromBody] FavoriteRequest request)
    {
        var media = await repo.SetIsFavorite(DUMMYUSER, httpRequest.GetBaseUrl(), id, request.IsFavorite);

        if (media == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(media);
    }

    static async Task<Results<Ok<IEnumerable<Comment>>, ForbidHttpResult>> GetComments(IMediaRepository repo, [FromRoute] Guid id) =>
        TypedResults.Ok(await repo.GetComments(DUMMYUSER, id));

    static async Task<Results<Ok<Comment>, NotFound, ForbidHttpResult>> AddComment(IMediaRepository repo, [FromRoute] Guid id, [FromBody] AddCommentRequest request)
    {
        var commentId = await repo.AddComment(DUMMYUSER, id, request.Body);

        if (commentId == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(await repo.GetComment(DUMMYUSER, (Guid)commentId));
    }

    static async Task<Results<Ok, NotFound, ForbidHttpResult>> SetGpsOverride(
        IMediaRepository repo,
        [FromRoute] Guid id,
        [FromBody] UpdateGpsRequest request)
    {
        // only used if truly new, otherwise media will be assigned location mathching these coords
        var newLocationId = Guid.CreateVersion7();

        var success = await repo.SetGpsOverride(
            DUMMYUSER,
            id,
            newLocationId,
            request.Latitude,
            request.Longitude
        );

        if (!success)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok();
    }

    static async Task<Results<Ok, NotFound, ForbidHttpResult>> BulkGpsOverride(
        IMediaRepository repo,
        [FromBody] BulkUpdateGpsRequest request
    )
    {
        // only used if truly new, otherwise media will be assigned location mathching these coords
        var newLocationId = Guid.CreateVersion7();

        var success = await repo.BulkSetGpsOverride(
            DUMMYUSER,
            request.MediaIds,
            newLocationId,
            request.GpsCoordinate.Latitude,
            request.GpsCoordinate.Longitude
        );

        if (!success)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok();
    }
}

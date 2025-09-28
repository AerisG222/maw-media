using System.Security.Claims;
using MawMedia.Authorization.Claims;
using MawMedia.Models;
using MawMedia.Routes.Extensions;
using MawMedia.Services;
using MawMedia.ViewModels;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace MawMedia.Routes;

public static class MediaRoutes
{
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

    static async Task<Results<Ok<IEnumerable<Media>>, ForbidHttpResult>> GetRandomMedia(
        IMediaRepository repo,
        ClaimsPrincipal user,
        HttpRequest request,
        [FromRoute] byte count
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await repo.GetRandomMedia(userId.Value, request.GetBaseUrl(), count))
            : TypedResults.Ok(Array.Empty<Media>().AsEnumerable());
    }

    static async Task<Results<Ok<Media>, NotFound, ForbidHttpResult>> GetMedia(
        IMediaRepository repo,
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

        var media = await repo.GetMedia(userId.Value, request.GetBaseUrl(), id);

        return media != null
            ? TypedResults.Ok(media)
            : TypedResults.NotFound();
    }

    static async Task<Results<Ok<Gps>, NotFound, ForbidHttpResult>> GetGps(
        IMediaRepository repo,
        ClaimsPrincipal user,
        [FromRoute] Guid id
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var gps = await repo.GetGps(userId.Value, id);

        return gps != null
            ? TypedResults.Ok(gps)
            : TypedResults.NotFound();
    }

    static async Task<IResult> GetMetadata(
        IMediaRepository repo,
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

        var metadata = await repo.GetMetadata(userId.Value, id);

        return metadata != null
            ? TypedResults.Ok(metadata)
            : TypedResults.NotFound();
    }

    static async Task<Results<Ok<Media>, NotFound, ForbidHttpResult>> FavoriteMedia(
        IMediaRepository repo,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        [FromRoute] Guid id,
        [FromBody] FavoriteRequest request)
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var media = await repo.SetIsFavorite(userId.Value, httpRequest.GetBaseUrl(), id, request.IsFavorite);

        return media != null
            ? TypedResults.Ok(media)
            : TypedResults.NotFound();
    }

    static async Task<Results<Ok<IEnumerable<Comment>>, ForbidHttpResult>> GetComments(
        IMediaRepository repo,
        ClaimsPrincipal user,
        [FromRoute] Guid id
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await repo.GetComments(userId.Value, id))
            : TypedResults.Ok(Array.Empty<Comment>().AsEnumerable());
    }

    static async Task<Results<Ok<Comment>, NotFound, ForbidHttpResult>> AddComment(
        IMediaRepository repo,
        ClaimsPrincipal user,
        [FromRoute] Guid id,
        [FromBody] AddCommentRequest request
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var commentId = await repo.AddComment(userId.Value, id, request.Body);

        return commentId != null
            ? TypedResults.Ok(await repo.GetComment(userId.Value, (Guid)commentId))
            : TypedResults.NotFound();
    }

    static async Task<Results<Ok, NotFound, ForbidHttpResult>> SetGpsOverride(
        IMediaRepository repo,
        ClaimsPrincipal user,
        [FromRoute] Guid id,
        [FromBody] UpdateGpsRequest request
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        // only used if truly new, otherwise media will be assigned location mathching these coords
        var newLocationId = Guid.CreateVersion7();

        var success = await repo.SetGpsOverride(
            userId.Value,
            id,
            newLocationId,
            request.Latitude,
            request.Longitude
        );

        return success
            ? TypedResults.Ok()
            : TypedResults.NotFound();
    }

    static async Task<Results<Ok, NotFound, ForbidHttpResult>> BulkGpsOverride(
        IMediaRepository repo,
        ClaimsPrincipal user,
        [FromBody] BulkUpdateGpsRequest request
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        // only used if truly new, otherwise media will be assigned location mathching these coords
        var newLocationId = Guid.CreateVersion7();

        var success = await repo.BulkSetGpsOverride(
            userId.Value,
            request.MediaIds,
            newLocationId,
            request.GpsCoordinate.Latitude,
            request.GpsCoordinate.Longitude
        );

        return success
            ? TypedResults.Ok()
            : TypedResults.NotFound();
    }
}

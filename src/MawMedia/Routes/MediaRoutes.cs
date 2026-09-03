using System.Security.Claims;
using MawMedia.Authorization.Claims;
using MawMedia.Models;
using MawMedia.Models.FaceRecognition;
using MawMedia.Routes.Extensions;
using MawMedia.Services.Abstractions;
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

        // face-recognition:read rather than media:read, like the rest of the face
        // read side, so the feature stays revocable per client without touching
        // media access
        group
            .MapGet("/{id}/faces", GetMediaFaces)
            .WithName("media-faces")
            .WithSummary("Get Faces for Media")
            .WithDescription("Gets the detected faces and their bounding boxes for a media item, so a client can draw where the people are")
            .RequireAuthorization(AuthorizationPolicies.FaceRecognitionReader);

        group
            .MapGet("/{id}/gps", GetGps)
            .WithName("media-gps")
            .WithSummary("Get GPS for Media")
            .WithDescription("Get GPS for media")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        // gated on media:read like the gps route above, and for the same reason:
        // naming the places a photograph was taken is browsing, not location
        // administration.  choosing one of them as a cover is the write, and that
        // still needs location:write - see PlaceRoutes.
        group
            .MapGet("/{id}/places", GetMediaPlaces)
            .WithName("media-places")
            .WithSummary("Places for Media")
            .WithDescription("Lists the places a media was taken - country first, then its state and city where the geocode resolved that deep. Each is a whole place, carrying its current cover, so a client can offer to replace one. Empty for media with no location.")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        group
            .MapPut("/{id}/favorite", FavoriteMedia)
            .WithName("favorite-media")
            .WithSummary("Favorite Media")
            .WithDescription("Sets whether this media is a favorite")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        group
            .MapGet("/{id}/comments", GetComments)
            .WithName("media-comments")
            .WithSummary("Get Media Comments")
            .WithDescription("Get media comments")
            .RequireAuthorization(AuthorizationPolicies.CommentsReader);

        group
            .MapGet("/{id}/comments/{commentId}", GetComment)
            .WithName("media-comment")
            .WithSummary("Get Media Comment")
            .WithDescription("Get a single media comment")
            .RequireAuthorization(AuthorizationPolicies.CommentsReader);

        group
            .MapPost("/{id}/comments", AddComment)
            .WithName("add-media-comment")
            .WithSummary("Add Media Comment")
            .WithDescription("Add comment for media")
            .RequireAuthorization(AuthorizationPolicies.CommentsWriter);

        group
            .MapPut("/{id}/gps", SetGpsOverride)
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
        [FromRoute] byte count,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await repo.GetRandomMedia(userId.Value, request.GetBaseUrl(), count, token))
            : TypedResults.Ok(Array.Empty<Media>().AsEnumerable());
    }

    static async Task<Results<Ok<Media>, NotFound, ForbidHttpResult>> GetMedia(
        IMediaRepository repo,
        ClaimsPrincipal user,
        HttpRequest request,
        [FromRoute] Guid id,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var media = await repo.GetMedia(userId.Value, request.GetBaseUrl(), id, token);

        return media != null
            ? TypedResults.Ok(media)
            : TypedResults.NotFound();
    }

    static async Task<Ok<IEnumerable<Face>>> GetMediaFaces(
        IFaceRepository repo,
        ClaimsPrincipal user,
        [FromRoute] Guid id,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        // an empty overlay rather than a 404: a media item with no faces and one
        // the caller cannot see are both "nothing to draw", and the media route
        // itself already tells those apart
        return userId != null
            ? TypedResults.Ok(await repo.GetMediaFaces(userId.Value, id, token))
            : TypedResults.Ok(Array.Empty<Face>().AsEnumerable());
    }

    static async Task<Results<Ok<Gps>, NotFound, ForbidHttpResult>> GetGps(
        IMediaRepository repo,
        ClaimsPrincipal user,
        [FromRoute] Guid id,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var gps = await repo.GetGps(userId.Value, id, token);

        return gps != null
            ? TypedResults.Ok(gps)
            : TypedResults.NotFound();
    }

    static async Task<Ok<IEnumerable<Place>>> GetMediaPlaces(
        IPlaceRepository repo,
        ClaimsPrincipal user,
        HttpRequest request,
        [FromRoute] Guid id,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        // an empty chain rather than a 404, matching the faces route: a media
        // with no location and one the caller cannot see are both "nowhere to
        // offer", and the media route itself already tells those apart
        return userId != null
            ? TypedResults.Ok(await repo.GetMediaPlaces(userId.Value, request.GetBaseUrl(), id, token))
            : TypedResults.Ok(Array.Empty<Place>().AsEnumerable());
    }

    static async Task<IResult> GetMetadata(
        IMediaRepository repo,
        ClaimsPrincipal user,
        HttpRequest request,
        [FromRoute] Guid id,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var metadata = await repo.GetMetadata(userId.Value, id, token);

        return metadata != null
            ? TypedResults.Ok(metadata)
            : TypedResults.NotFound();
    }

    static async Task<Results<Ok<Media>, NotFound, ForbidHttpResult>> FavoriteMedia(
        IMediaRepository repo,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        [FromRoute] Guid id,
        [FromBody] FavoriteRequest request,
        CancellationToken token)
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var media = await repo.SetIsFavorite(userId.Value, httpRequest.GetBaseUrl(), id, request.IsFavorite, token);

        return media != null
            ? TypedResults.Ok(media)
            : TypedResults.NotFound();
    }

    static async Task<Results<Ok<IEnumerable<Comment>>, ForbidHttpResult>> GetComments(
        IMediaRepository repo,
        ClaimsPrincipal user,
        [FromRoute] Guid id,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await repo.GetComments(userId.Value, id, token))
            : TypedResults.Ok(Array.Empty<Comment>().AsEnumerable());
    }

    static async Task<Results<Ok<Comment>, NotFound, ForbidHttpResult>> GetComment(
        IMediaRepository repo,
        ClaimsPrincipal user,
        [FromRoute] Guid id,
        [FromRoute] Guid commentId,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var comment = await repo.GetComment(userId.Value, commentId, token);

        return comment != null
            ? TypedResults.Ok(comment)
            : TypedResults.NotFound();
    }

    static async Task<Results<Created<Comment>, NotFound, ForbidHttpResult>> AddComment(
        IMediaRepository repo,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        [FromRoute] Guid id,
        [FromBody] AddCommentRequest request,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var commentId = await repo.AddComment(userId.Value, id, request.Body, token);

        if (commentId == null)
        {
            return TypedResults.NotFound();
        }

        var comment = await repo.GetComment(userId.Value, commentId.Value, token);

        // point at the canonical single-comment resource (GetComment)
        return TypedResults.Created($"{httpRequest.Path}/{commentId.Value}", comment);
    }

    static async Task<Results<Ok, NotFound, ForbidHttpResult>> SetGpsOverride(
        IMediaRepository repo,
        ClaimsPrincipal user,
        [FromRoute] Guid id,
        [FromBody] UpdateGpsRequest request,
        CancellationToken token
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
            request.Longitude,
            token
        );

        return success
            ? TypedResults.Ok()
            : TypedResults.NotFound();
    }

    static async Task<Results<Ok, NotFound, ForbidHttpResult>> BulkGpsOverride(
        IMediaRepository repo,
        ClaimsPrincipal user,
        [FromBody] BulkUpdateGpsRequest request,
        CancellationToken token
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
            request.GpsCoordinate.Longitude,
            token
        );

        return success
            ? TypedResults.Ok()
            : TypedResults.NotFound();
    }
}

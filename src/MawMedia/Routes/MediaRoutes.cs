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

        // DELETE on the same resource the PUT above writes, so the pair reads as one
        // thing: the override for this media, set or removed
        group
            .MapDelete("/{id}/gps", ClearGpsOverride)
            .WithName("clear-media-gps-override")
            .WithSummary("Clear GPS Override for Media")
            .WithDescription("Remove the GPS override for this media, so it falls back to the location its file recorded")
            .RequireAuthorization(AuthorizationPolicies.MediaWriter);

        group
            .MapPost("/bulk-gps-override", BulkGpsOverride)
            .WithName("bulk-set-gps-override")
            .WithSummary("Bulk GPS Override")
            .WithDescription("Set the GPS override for many media items at once")
            .RequireAuthorization(AuthorizationPolicies.MediaWriter);

        // POST rather than a DELETE carrying the id list.  a DELETE body has no
        // defined meaning in http, and proxies and clients are entitled to drop it -
        // which here would mean the request arriving with nothing to clear and
        // answering 200 for having done so
        group
            .MapPost("/bulk-gps-override/clear", BulkClearGpsOverride)
            .WithName("bulk-clear-gps-override")
            .WithSummary("Bulk Clear GPS Override")
            .WithDescription("Remove the GPS override from many media items at once")
            .RequireAuthorization(AuthorizationPolicies.MediaWriter);

        // restrictions - media visible to fewer roles than their category grants.
        // admin only, which the database enforces; these routes ask for the write
        // scope as every media change does, and answer 403 for a non admin.
        //
        // the GET is under the write scope as well: it exists for the admin screen
        // that edits restrictions, and a reader has no use for it.
        group
            .MapGet("/{id}/roles", GetMediaRoles)
            .WithName("get-media-roles")
            .WithSummary("Get Media Restriction")
            .WithDescription("The roles this media is restricted to - empty when it follows its category")
            .RequireAuthorization(AuthorizationPolicies.MediaWriter);

        group
            .MapPut("/{id}/roles", SetMediaRoles)
            .WithName("set-media-roles")
            .WithSummary("Restrict Media")
            .WithDescription("Restrict this media to the named roles, replacing any restriction it had")
            .RequireAuthorization(AuthorizationPolicies.MediaWriter);

        group
            .MapDelete("/{id}/roles", ClearMediaRoles)
            .WithName("clear-media-roles")
            .WithSummary("Unrestrict Media")
            .WithDescription("Remove this media's restriction, so it follows its category again")
            .RequireAuthorization(AuthorizationPolicies.MediaWriter);

        // POST rather than a DELETE carrying ids, for the reason the bulk gps clear
        // gives: a DELETE body may be dropped on the way, and would arrive as a
        // request to clear nothing
        group
            .MapPost("/bulk-roles", BulkSetMediaRoles)
            .WithName("bulk-set-media-roles")
            .WithSummary("Bulk Restrict Media")
            .WithDescription("Restrict many media to the named roles at once - all or nothing")
            .RequireAuthorization(AuthorizationPolicies.MediaWriter);

        group
            .MapPost("/bulk-roles/clear", BulkClearMediaRoles)
            .WithName("bulk-clear-media-roles")
            .WithSummary("Bulk Unrestrict Media")
            .WithDescription("Remove the restriction from many media at once")
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

    // answers the way SetGpsOverride does - 404 for a media the caller may not
    // change, whether or not it exists - so the pair cannot be used to tell an
    // unknown id from a forbidden one
    static async Task<Results<Ok, NotFound, ForbidHttpResult>> ClearGpsOverride(
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

        var success = await repo.ClearGpsOverride(userId.Value, id, token);

        return success
            ? TypedResults.Ok()
            : TypedResults.NotFound();
    }

    static async Task<Results<Ok, NotFound, ForbidHttpResult>> BulkClearGpsOverride(
        IMediaRepository repo,
        ClaimsPrincipal user,
        [FromBody] BulkClearGpsRequest request,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var success = await repo.BulkClearGpsOverride(userId.Value, request.MediaIds, token);

        return success
            ? TypedResults.Ok()
            : TypedResults.NotFound();
    }

    static async Task<Results<Ok<IReadOnlyList<string>>, NotFound, ForbidHttpResult>> GetMediaRoles(
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

        var result = await repo.GetMediaRoles(userId.Value, id, token);

        return result.Outcome switch
        {
            MediaRestrictionOutcome.Ok => TypedResults.Ok(result.Roles),
            MediaRestrictionOutcome.NotAdmin => TypedResults.Forbid(),
            _ => TypedResults.NotFound()
        };
    }

    static async Task<Results<Ok<IReadOnlyList<string>>, NotFound, BadRequest<IReadOnlyList<MediaRestrictionProblem>>, ForbidHttpResult>> SetMediaRoles(
        IMediaRepository repo,
        ClaimsPrincipal user,
        [FromRoute] Guid id,
        [FromBody] MediaRolesRequest request,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var result = await repo.SetMediaRoles(userId.Value, [id], request.Roles ?? [], token);

        switch (result.Outcome)
        {
            case MediaRestrictionOutcome.NotAdmin:
                return TypedResults.Forbid();

            // a single media that does not exist is a 404, not a list of one
            // problem - this route names one resource, and it is not there
            case MediaRestrictionOutcome.Invalid when result.Problems.All(p => p.Reason == "not_found"):
                return TypedResults.NotFound();

            case MediaRestrictionOutcome.Invalid:
                return TypedResults.BadRequest(result.Problems);
        }

        // read back rather than echoing the request, so the client sees the
        // restriction as stored - names deduplicated and in a stable order
        return TypedResults.Ok((await repo.GetMediaRoles(userId.Value, id, token)).Roles);
    }

    static async Task<Results<Ok, NotFound, ForbidHttpResult>> ClearMediaRoles(
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

        return await repo.ClearMediaRoles(userId.Value, [id], token)
            ? TypedResults.Ok()
            : TypedResults.Forbid();
    }

    static async Task<Results<Ok, NotFound, BadRequest<IReadOnlyList<MediaRestrictionProblem>>, ForbidHttpResult>> BulkSetMediaRoles(
        IMediaRepository repo,
        ClaimsPrincipal user,
        [FromBody] BulkMediaRolesRequest request,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var result = await repo.SetMediaRoles(userId.Value, request.MediaIds ?? [], request.Roles ?? [], token);

        // an id that names no media is reported among the problems here rather
        // than as a 404: the request as a whole exists, and an admin screen marks
        // each photo it could not restrict
        return result.Outcome switch
        {
            MediaRestrictionOutcome.Ok => TypedResults.Ok(),
            MediaRestrictionOutcome.NotAdmin => TypedResults.Forbid(),
            _ => TypedResults.BadRequest(result.Problems)
        };
    }

    static async Task<Results<Ok, NotFound, ForbidHttpResult>> BulkClearMediaRoles(
        IMediaRepository repo,
        ClaimsPrincipal user,
        [FromBody] BulkClearMediaRolesRequest request,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        return await repo.ClearMediaRoles(userId.Value, request.MediaIds ?? [], token)
            ? TypedResults.Ok()
            : TypedResults.Forbid();
    }
}

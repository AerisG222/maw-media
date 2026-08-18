using System.Security.Claims;
using MawMedia.Authorization.Claims;
using MawMedia.Models.FaceRecognition;
using MawMedia.Routes.Extensions;
using MawMedia.Services;
using MawMedia.Services.Abstractions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace MawMedia.Routes;

public static class FaceRoutes
{
    // faces are the volume driver: the largest cluster upstream holds roughly
    // 40,000 of them, and a face serialises to a couple of hundred bytes, so an
    // unbounded batch would arrive as a multi megabyte body that is then
    // deserialised and re-serialised as jsonb.  1,000 keeps a request small
    // enough that a retry costs nothing.
    public const int MAX_FACES = 1000;
    public const int MAX_DELETIONS = 1000;

    // a face crop is a thumbnail; anything larger is a mistake upstream
    public const int MAX_IMAGE_BYTES = 2 * 1024 * 1024;

    public static RouteGroupBuilder MapFaceRoutes(this RouteGroupBuilder group)
    {
        group
            .MapPost("/sync", SyncFaces)
            .WithName("faces-sync")
            .WithSummary("Sync Faces")
            .WithDescription("Publishes a batch of detected faces from the recognition pipeline")
            .RequireAuthorization(AuthorizationPolicies.FaceRecognitionPublisher);

        group
            .MapPost("/deletions", DeleteFaces)
            .WithName("faces-deletions")
            .WithSummary("Delete Faces")
            .WithDescription("Removes faces that no longer exist in the recognition pipeline")
            .RequireAuthorization(AuthorizationPolicies.FaceRecognitionPublisher);

        // the cropped image is produced by maw-media-ai and stored verbatim.
        // keeping the crop upstream is deliberate: it avoids an imaging
        // dependency here, and the pipeline already has the original open when
        // it picks a preferred face.
        group
            .MapPut("/{id:guid}/image", PutImage)
            .WithName("faces-put-image")
            .WithSummary("Publish Face Image")
            .WithDescription("Stores the cropped image for a face, replacing any existing one")
            .RequireAuthorization(AuthorizationPolicies.FaceRecognitionPublisher);

        group
            .MapGet("/{id:guid}/image", GetImage)
            .WithName("faces-get-image")
            .WithSummary("Face Image")
            .WithDescription("Returns the cropped image previously published for a face")
            .RequireAuthorization(AuthorizationPolicies.User);

        return group;
    }

    static async Task<Results<NoContent, BadRequest<string>, NotFound, ForbidHttpResult>> PutImage(
        ClaimsPrincipal user,
        IFaceRepository repo,
        IFaceImageStore store,
        HttpRequest request,
        [FromRoute] Guid id,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.Forbid();
        }

        if (!string.Equals(request.ContentType, FaceImageStore.CONTENT_TYPE, StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.BadRequest($"Content-Type must be {FaceImageStore.CONTENT_TYPE}.");
        }

        if (request.ContentLength is null or 0)
        {
            return TypedResults.BadRequest("A Content-Length is required and must be greater than zero.");
        }

        if (request.ContentLength > MAX_IMAGE_BYTES)
        {
            return TypedResults.BadRequest($"An image may be at most {MAX_IMAGE_BYTES} bytes.");
        }

        // refuse an id that was never published, so a typo cannot leave an
        // orphan file in a directory nothing enumerates
        if (!await repo.FaceExists(userId.Value, id, token))
        {
            return TypedResults.NotFound();
        }

        await store.Save(id, request.Body, token);

        return TypedResults.NoContent();
    }

    static Results<FileStreamHttpResult, NotFound, ForbidHttpResult> GetImage(
        ClaimsPrincipal user,
        IFaceImageStore store,
        [FromRoute] Guid id
    )
    {
        if (user.GetMediaUserId() == null)
        {
            return TypedResults.Forbid();
        }

        var content = store.Open(id);

        return content == null
            ? TypedResults.NotFound()
            : TypedResults.File(content, FaceImageStore.CONTENT_TYPE);
    }

    static async Task<Results<Ok<IEnumerable<FaceSyncResult>>, BadRequest<string>, ForbidHttpResult>> SyncFaces(
        ClaimsPrincipal user,
        IFaceRepository repo,
        [FromBody] FaceSync[] faces,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.Forbid();
        }

        if (faces.Length > MAX_FACES)
        {
            return TypedResults.BadRequest($"A batch may contain at most {MAX_FACES} faces; received {faces.Length}.");
        }

        return (await repo.SyncFaces(userId.Value, faces, token)).ToHttpResult();
    }

    static async Task<Results<Ok<IEnumerable<FaceSyncResult>>, BadRequest<string>, ForbidHttpResult>> DeleteFaces(
        ClaimsPrincipal user,
        IFaceRepository repo,
        [FromBody] Guid[] faceIds,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.Forbid();
        }

        if (faceIds.Length > MAX_DELETIONS)
        {
            return TypedResults.BadRequest($"A batch may contain at most {MAX_DELETIONS} ids; received {faceIds.Length}.");
        }

        return (await repo.DeleteFaces(userId.Value, faceIds, token)).ToHttpResult();
    }
}

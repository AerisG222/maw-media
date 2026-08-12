using System.Security.Claims;
using MawMedia.Authorization.Claims;
using MawMedia.Models.FaceRecognition;
using MawMedia.Routes.Extensions;
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

        return group;
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

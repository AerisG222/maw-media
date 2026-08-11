using System.Security.Claims;
using MawMedia.Authorization.Claims;
using MawMedia.Routes.Extensions;
using MawMedia.Models.FaceRecognition;
using MawMedia.Services.Abstractions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace MawMedia.Routes;

public static class PersonRoutes
{
    // batch sizes are bounded so a publish cannot arrive as a single enormous
    // body - the largest cluster upstream holds roughly 40,000 faces, and the
    // request is deserialised and then re-serialised as jsonb on the way to
    // postgres.  constants rather than configuration: they are not expected to
    // differ by environment, and are easy to promote later if that changes.
    public const int MAX_PERSONS = 500;
    public const int MAX_DELETIONS = 1000;

    public static RouteGroupBuilder MapPersonRoutes(this RouteGroupBuilder group)
    {
        group
            .MapPost("/sync", SyncPersons)
            .WithName("persons-sync")
            .WithSummary("Sync People")
            .WithDescription("Publishes a batch of people (face clusters) from the recognition pipeline")
            .RequireAuthorization(AuthorizationPolicies.FaceRecognitionPublisher);

        group
            .MapPost("/deletions", DeletePersons)
            .WithName("persons-deletions")
            .WithSummary("Delete People")
            .WithDescription("Removes people that no longer exist in the recognition pipeline")
            .RequireAuthorization(AuthorizationPolicies.FaceRecognitionPublisher);

        return group;
    }

    static async Task<Results<Ok<IEnumerable<FaceSyncResult>>, BadRequest<string>, ForbidHttpResult>> SyncPersons(
        ClaimsPrincipal user,
        IFaceRepository repo,
        [FromBody] PersonSync[] persons,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.Forbid();
        }

        if (persons.Length > MAX_PERSONS)
        {
            return TypedResults.BadRequest($"A batch may contain at most {MAX_PERSONS} people; received {persons.Length}.");
        }

        return (await repo.SyncPersons(userId.Value, persons, token)).ToHttpResult();
    }

    static async Task<Results<Ok<IEnumerable<FaceSyncResult>>, BadRequest<string>, ForbidHttpResult>> DeletePersons(
        ClaimsPrincipal user,
        IFaceRepository repo,
        [FromBody] Guid[] personIds,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.Forbid();
        }

        if (personIds.Length > MAX_DELETIONS)
        {
            return TypedResults.BadRequest($"A batch may contain at most {MAX_DELETIONS} ids; received {personIds.Length}.");
        }

        return (await repo.DeletePersons(userId.Value, personIds, token)).ToHttpResult();
    }
}

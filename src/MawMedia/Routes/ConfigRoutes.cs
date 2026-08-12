using System.Security.Claims;
using MawMedia.Authorization.Claims;
using MawMedia.Models;
using MawMedia.Models.FaceRecognition;
using MawMedia.Routes.Extensions;
using MawMedia.Services.Abstractions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace MawMedia.Routes;

public static class ConfigRoutes
{
    // there will only ever be a handful of these
    public const int MAX_PERSON_STATUSES = 100;

    public static RouteGroupBuilder MapConfigRoutes(this RouteGroupBuilder group)
    {
        group
            .MapGet("/scales", GetScales)
            .WithName("config-scales")
            .WithSummary("Scales")
            .WithDescription("Lists available scales")
            .RequireAuthorization(AuthorizationPolicies.User);

        // person statuses live here beside scales because they are the same kind
        // of thing - a small lookup the api exposes.  the difference is that
        // maw-media-ai owns the values, so they arrive by publish rather than by
        // seed, which is why a write endpoint sits in the config group.
        group
            .MapPost("/person-statuses/sync", SyncPersonStatuses)
            .WithName("config-person-statuses-sync")
            .WithSummary("Sync Person Statuses")
            .WithDescription("Publishes the person status lookup from the recognition pipeline")
            .RequireAuthorization(AuthorizationPolicies.FaceRecognitionPublisher);

        return group;
    }

    static async Task<Results<Ok<IEnumerable<FaceSyncResult>>, BadRequest<string>, ForbidHttpResult>> SyncPersonStatuses(
        ClaimsPrincipal user,
        IFaceRepository repo,
        [FromBody] PersonStatusSync[] statuses,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.Forbid();
        }

        if (statuses.Length > MAX_PERSON_STATUSES)
        {
            return TypedResults.BadRequest($"A batch may contain at most {MAX_PERSON_STATUSES} statuses; received {statuses.Length}.");
        }

        return (await repo.SyncPersonStatuses(userId.Value, statuses, token)).ToHttpResult();
    }

    static async Task<Results<Ok<IEnumerable<Scale>>, ForbidHttpResult>> GetScales(IConfigRepository repo, CancellationToken token) =>
        TypedResults.Ok(await repo.GetScales(token));
}

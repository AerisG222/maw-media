using System.Security.Claims;
using MawMedia.Authorization.Claims;
using MawMedia.Routes.Extensions;
using MawMedia.Models;
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

    // matches the category search page size so a client can render both result
    // sets with the same grid and the same "load more" behaviour
    const int MEDIA_LIMIT = 24;

    public static RouteGroupBuilder MapPersonRoutes(this RouteGroupBuilder group)
    {
        // returned whole rather than paged: the set is a few hundred at most and
        // clients filter it locally, which beats a round trip per keystroke
        group
            .MapGet("/", GetPersons)
            .WithName("persons-list")
            .WithSummary("People")
            .WithDescription("Lists the people appearing in media the caller can access")
            .RequireAuthorization(AuthorizationPolicies.FaceRecognitionReader);

        // the drill-in behind a person in the picker.  paged, unlike the list
        // above: one person can appear in thousands of media.
        group
            .MapGet("/{id:guid}/media", GetPersonMedia)
            .WithName("person-media")
            .WithSummary("Media for a Person")
            .WithDescription("Lists the media a person appears in that the caller can access. Pass f=true for favorites only, and seed=<number> for a shuffled order that stays stable across pages.")
            .RequireAuthorization(AuthorizationPolicies.FaceRecognitionReader);

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

    static async Task<Results<Ok<IEnumerable<Person>>, ForbidHttpResult>> GetPersons(
        ClaimsPrincipal user,
        IFaceRepository repo,
        HttpRequest request,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await repo.GetPersons(userId.Value, request.GetBaseUrl(), token))
            : TypedResults.Ok(Array.Empty<Person>().AsEnumerable());
    }

    static async Task<Results<Ok<SearchResult<Media>>, NotFound, BadRequest<string>>> GetPersonMedia(
        ClaimsPrincipal user,
        IFaceRepository repo,
        HttpRequest request,
        [FromRoute] Guid id,
        CancellationToken token,
        [FromQuery] int o = 0,
        [FromQuery] bool f = false,
        [FromQuery] long? seed = null
    )
    {
        if (o < 0)
        {
            return TypedResults.BadRequest("Offset must be greater than or equal to 0.");
        }

        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var result = await repo.GetPersonMedia(userId.Value, request.GetBaseUrl(), id, o, MEDIA_LIMIT, f, seed, token);

        // 404 rather than an empty first page, and 404 rather than 403 for a
        // person the caller may not see - the same rule the face image download
        // follows, so a caller cannot probe for people the picker hid from them.
        // an empty page past the first is a normal answer, not a missing person.
        //
        // a favourites filter is exempt: "this person has no favourites" is a
        // real answer about a person the caller can see, and answering 404 would
        // make an empty filter look like a broken link.  seed is not exempt - it
        // reorders rather than filters, so an empty first page still means there
        // is nothing to show.
        return o == 0 && !f && !result.Results.Any()
            ? TypedResults.NotFound()
            : TypedResults.Ok(result);
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

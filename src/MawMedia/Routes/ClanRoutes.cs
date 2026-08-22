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

public static class ClanRoutes
{
    // a clan is a shortcut, not a second copy of the person list - a caller
    // wanting everyone already has the person list.  the bound keeps a runaway
    // client from writing a membership row per person per clan.
    public const int MAX_CLAN_PERSONS = 100;
    public const int MAX_NAME_LENGTH = 100;

    // matches the person media and category search page sizes so a client can
    // render all three with one grid
    const int MEDIA_LIMIT = 24;

    const string UNKNOWN_PERSON = "One or more of the supplied people could not be found.";
    const string DUPLICATE_NAME = "You already have a clan with that name.";

    public static RouteGroupBuilder MapClanRoutes(this RouteGroupBuilder group)
    {
        group
            .MapGet("/", GetClans)
            .WithName("clans")
            .WithSummary("Clans")
            .WithDescription("Lists the caller's clans and their members")
            .RequireAuthorization(AuthorizationPolicies.FaceRecognitionReader);

        group
            .MapPost("/", CreateClan)
            .WithName("clan-create")
            .WithSummary("Create Clan")
            .WithDescription("Creates a clan from a set of people")
            .RequireAuthorization(AuthorizationPolicies.FaceRecognitionReader);

        group
            .MapGet("/{id:guid}", GetClan)
            .WithName("clan")
            .WithSummary("Clan")
            .WithDescription("Gets a single clan and its members")
            .RequireAuthorization(AuthorizationPolicies.FaceRecognitionReader);

        group
            .MapPut("/{id:guid}", UpdateClan)
            .WithName("clan-update")
            .WithSummary("Update Clan")
            .WithDescription("Renames a clan, and replaces its membership when people are supplied")
            .RequireAuthorization(AuthorizationPolicies.FaceRecognitionReader);

        group
            .MapPut("/{id:guid}/persons", SetClanPersons)
            .WithName("clan-set-persons")
            .WithSummary("Set Clan Members")
            .WithDescription("Replaces the people in a clan with the supplied set")
            .RequireAuthorization(AuthorizationPolicies.FaceRecognitionReader);

        group
            .MapDelete("/{id:guid}", DeleteClan)
            .WithName("clan-delete")
            .WithSummary("Delete Clan")
            .WithDescription("Removes a clan. The people in it are untouched")
            .RequireAuthorization(AuthorizationPolicies.FaceRecognitionReader);

        group
            .MapGet("/{id:guid}/media", GetClanMedia)
            .WithName("clan-media")
            .WithSummary("Media for a Clan")
            .WithDescription("Lists the media any member of the clan appears in. Pass f=true for favorites only, and seed=<number> for a shuffled order that stays stable across pages.")
            .RequireAuthorization(AuthorizationPolicies.FaceRecognitionReader);

        return group;
    }

    static async Task<Ok<IEnumerable<Clan>>> GetClans(
        ClaimsPrincipal user,
        IFaceRepository repo,
        HttpRequest request,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await repo.GetClans(userId.Value, request.GetBaseUrl(), token))
            : TypedResults.Ok(Array.Empty<Clan>().AsEnumerable());
    }

    static async Task<Results<Ok<Clan>, NotFound>> GetClan(
        ClaimsPrincipal user,
        IFaceRepository repo,
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

        var clan = await repo.GetClan(userId.Value, request.GetBaseUrl(), id, token);

        return clan == null
            ? TypedResults.NotFound()
            : TypedResults.Ok(clan);
    }

    static async Task<Results<Created<Clan>, BadRequest<string>, Conflict<string>, NotFound>> CreateClan(
        ClaimsPrincipal user,
        IFaceRepository repo,
        HttpRequest request,
        [FromBody] ClanRequest clanRequest,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var personIds = clanRequest.PersonIds ?? [];
        var invalid = Validate(clanRequest.Name, personIds);

        if (invalid != null)
        {
            return TypedResults.BadRequest(invalid);
        }

        var (clanId, outcome) = await repo.CreateClan(userId.Value, clanRequest.Name, personIds, token);

        if (outcome != ClanOutcome.Applied || clanId == null)
        {
            // spelled out rather than sharing Reject below, because a create
            // answers Created rather than Ok and the two unions have no common
            // type the compiler will infer
            return outcome switch
            {
                ClanOutcome.UnknownPerson => TypedResults.BadRequest(UNKNOWN_PERSON),
                ClanOutcome.DuplicateName => TypedResults.Conflict(DUPLICATE_NAME),
                _ => TypedResults.NotFound()
            };
        }

        var clan = await repo.GetClan(userId.Value, request.GetBaseUrl(), clanId.Value, token);

        return clan == null
            ? TypedResults.NotFound()
            : TypedResults.Created($"/api/v1/clans/{clanId}", clan);
    }

    static async Task<Results<Ok<Clan>, BadRequest<string>, Conflict<string>, NotFound>> UpdateClan(
        ClaimsPrincipal user,
        IFaceRepository repo,
        HttpRequest request,
        [FromRoute] Guid id,
        [FromBody] ClanRequest clanRequest,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var invalid = Validate(clanRequest.Name, clanRequest.PersonIds ?? []);

        if (invalid != null)
        {
            return TypedResults.BadRequest(invalid);
        }

        var outcome = await repo.UpdateClan(userId.Value, id, clanRequest.Name, token);

        if (outcome != ClanOutcome.Applied)
        {
            return Reject(outcome);
        }

        // membership is optional on a rename: a client editing only the name
        // should not have to resend the whole member list, and an omitted list
        // means "leave it alone" rather than "empty it"
        if (clanRequest.PersonIds != null)
        {
            var members = await repo.SetClanPersons(userId.Value, id, clanRequest.PersonIds, token);

            if (members != ClanOutcome.Applied)
            {
                return Reject(members);
            }
        }

        var clan = await repo.GetClan(userId.Value, request.GetBaseUrl(), id, token);

        return clan == null
            ? TypedResults.NotFound()
            : TypedResults.Ok(clan);
    }

    static async Task<Results<Ok<Clan>, BadRequest<string>, Conflict<string>, NotFound>> SetClanPersons(
        ClaimsPrincipal user,
        IFaceRepository repo,
        HttpRequest request,
        [FromRoute] Guid id,
        [FromBody] ClanPersonsRequest personsRequest,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var personIds = personsRequest.PersonIds ?? [];

        if (personIds.Length > MAX_CLAN_PERSONS)
        {
            return TypedResults.BadRequest($"A clan may hold at most {MAX_CLAN_PERSONS} people; received {personIds.Length}.");
        }

        var outcome = await repo.SetClanPersons(userId.Value, id, personIds, token);

        if (outcome != ClanOutcome.Applied)
        {
            return Reject(outcome);
        }

        var clan = await repo.GetClan(userId.Value, request.GetBaseUrl(), id, token);

        return clan == null
            ? TypedResults.NotFound()
            : TypedResults.Ok(clan);
    }

    static async Task<Results<NoContent, NotFound>> DeleteClan(
        ClaimsPrincipal user,
        IFaceRepository repo,
        [FromRoute] Guid id,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        return await repo.DeleteClan(userId.Value, id, token) == ClanOutcome.Applied
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
    }

    static async Task<Results<Ok<SearchResult<Media>>, NotFound, BadRequest<string>>> GetClanMedia(
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

        var result = await repo.GetClanMedia(userId.Value, request.GetBaseUrl(), id, o, MEDIA_LIMIT, f, seed, token);

        // an empty first page is a 404 for the same reason the person drill-in
        // treats one that way, with the same two exemptions: a favourites filter
        // can legitimately come back empty, and a page past the end is a normal
        // answer.  an empty *clan* would also read as 404 here, which is why a
        // client that needs to tell them apart reads the clan itself.
        return o == 0 && !f && !result.Results.Any()
            ? TypedResults.NotFound()
            : TypedResults.Ok(result);
    }

    static string? Validate(string? name, Guid[] personIds)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "A clan needs a name.";
        }

        if (name.Length > MAX_NAME_LENGTH)
        {
            return $"A clan name may be at most {MAX_NAME_LENGTH} characters; received {name.Length}.";
        }

        return personIds.Length > MAX_CLAN_PERSONS
            ? $"A clan may hold at most {MAX_CLAN_PERSONS} people; received {personIds.Length}."
            : null;
    }

    // an unknown person is the caller's mistake (400), a taken name is a
    // collision with their own data (409), and anything else means the clan is
    // not theirs to touch - which must be indistinguishable from it not existing
    static Results<Ok<Clan>, BadRequest<string>, Conflict<string>, NotFound> Reject(ClanOutcome outcome) =>
        outcome switch
        {
            ClanOutcome.UnknownPerson => TypedResults.BadRequest(UNKNOWN_PERSON),
            ClanOutcome.DuplicateName => TypedResults.Conflict(DUPLICATE_NAME),
            _ => TypedResults.NotFound()
        };
}

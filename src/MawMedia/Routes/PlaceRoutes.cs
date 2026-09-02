using System.Security.Claims;
using MawMedia.Authorization.Claims;
using MawMedia.Routes.Extensions;
using MawMedia.Models;
using MawMedia.Services.Abstractions;
using MawMedia.ViewModels;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace MawMedia.Routes;

public static class PlaceRoutes
{
    // matches the person and category search page size so a client can render all
    // three result sets with the same grid and the same "load more" behaviour
    const int MEDIA_LIMIT = 50;

    public static RouteGroupBuilder MapPlaceRoutes(this RouteGroupBuilder group)
    {
        // gated on MediaReader rather than LocationReader.  location:read is the
        // geocode maintenance scope - it lets the correction worker enumerate
        // coordinates missing metadata - and browsing is plain media browsing.
        // reusing it would hand every browse client the worker's permissions.

        // returned whole rather than paged: each request is scoped to one parent,
        // so the largest answer is one country's states or one state's cities
        group
            .MapGet("/", GetPlaces)
            .WithName("places-list")
            .WithSummary("Places")
            .WithDescription("Lists places the caller has media in. Omit parent for the countries; pass parent=<id> to drill into a place's children, and kind=country|state|city to filter a mixed listing.")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        group
            .MapGet("/{id:guid}", GetPlace)
            .WithName("place-get")
            .WithSummary("Place")
            .WithDescription("Gets a single place, including how many of the caller's media were taken there.")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        group
            .MapGet("/{id:guid}/ancestors", GetPlaceAncestors)
            .WithName("place-ancestors")
            .WithSummary("Ancestors of a Place")
            .WithDescription("Lists the breadcrumb above a place, country first and including the place itself.")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        // the drill-in behind a place tile.  paged, unlike the listing above: one
        // country holds tens of thousands of media.
        group
            .MapGet("/{id:guid}/media", GetPlaceMedia)
            .WithName("place-media")
            .WithSummary("Media for a Place")
            .WithDescription("Lists the media taken at a place that the caller can access, including everything in its states and cities. Pass f=true for favorites only, and seed=<number> for a shuffled order that stays stable across pages.")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        // the other half of the drill-in: the same media, rolled up to the
        // categories holding them, so a place screen can toggle between them
        // choosing a cover publishes a copy of the photograph to a directory
        // served with no authorization check.  that makes it a publication rather
        // than an edit, so it sits behind LocationWriter - the location
        // administration scope - and media.set_place_cover checks admin again in
        // the database.  browsing for a candidate needs no new endpoint: the media
        // route above already lists exactly the photographs eligible to be chosen.
        group
            .MapPut("/{id:guid}/cover", SetPlaceCover)
            .WithName("place-cover-set")
            .WithSummary("Set a Place Cover")
            .WithDescription("Publishes one of the caller's photographs as the place's cover image. The media must be at this place or beneath it. The published copy is served publicly, without authorization.")
            .RequireAuthorization(AuthorizationPolicies.LocationWriter);

        group
            .MapDelete("/{id:guid}/cover", ClearPlaceCover)
            .WithName("place-cover-clear")
            .WithSummary("Clear a Place Cover")
            .WithDescription("Removes the place's cover image and deletes the published copy.")
            .RequireAuthorization(AuthorizationPolicies.LocationWriter);

        // reshaping the tree.  the derived hierarchy is only as good as the
        // geocoder, and these are how an admin corrects it - behind
        // LocationWriter, with the database checking admin again.
        group
            .MapPost("/{id:guid}/merge", MergePlaces)
            .WithName("place-merge")
            .WithSummary("Merge a Place")
            .WithDescription("Folds another place into this one and deletes it. Both must be the same kind, but they need not share a parent. Everything pointing at the merged place - locations, children and geocode aliases - is repointed here.")
            .RequireAuthorization(AuthorizationPolicies.LocationWriter);

        group
            .MapPut("/{id:guid}/parent", SetPlaceParent)
            .WithName("place-parent")
            .WithSummary("Move a Place")
            .WithDescription("Moves a place under a different parent, or to the root when parentId is null. The parent must sit above the place in the hierarchy.")
            .RequireAuthorization(AuthorizationPolicies.LocationWriter);

        group
            .MapGet("/{id:guid}/categories", GetPlaceCategories)
            .WithName("place-categories")
            .WithSummary("Categories for a Place")
            .WithDescription("Lists the categories holding media taken at a place. Pass f=true for categories the caller has favorited, or that hold media they have favorited.")
            .RequireAuthorization(AuthorizationPolicies.MediaReader);

        return group;
    }

    static async Task<Results<Ok<IEnumerable<Place>>, BadRequest<string>>> GetPlaces(
        ClaimsPrincipal user,
        IPlaceRepository repo,
        HttpRequest request,
        CancellationToken token,
        [FromQuery] Guid? parent = null,
        [FromQuery] string? kind = null
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.Ok(Array.Empty<Place>().AsEnumerable());
        }

        // an empty listing is a real answer here, unlike the drill-ins below - a
        // place whose children the caller cannot see is legitimately a leaf to
        // them, and 404 would make the last level of every browse look broken
        return TypedResults.Ok(await repo.GetPlaces(userId.Value, request.GetBaseUrl(), parent, kind, token));
    }

    static async Task<Results<Ok<Place>, NotFound>> GetPlace(
        ClaimsPrincipal user,
        IPlaceRepository repo,
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

        var place = await repo.GetPlace(userId.Value, request.GetBaseUrl(), id, token);

        // 404 rather than 403 for a place the caller may see nothing at, matching
        // the person read side - a 403 would confirm the place exists
        return place == null
            ? TypedResults.NotFound()
            : TypedResults.Ok(place);
    }

    static async Task<Results<Ok<IEnumerable<PlaceAncestor>>, NotFound>> GetPlaceAncestors(
        ClaimsPrincipal user,
        IPlaceRepository repo,
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

        // the place itself is checked rather than the chain, so a caller cannot
        // read a breadcrumb for a place the listing would not have shown them.
        // once past that the names are not privileged - the media behind each rung
        // is still filtered by its own request.
        if (await repo.GetPlace(userId.Value, request.GetBaseUrl(), id, token) == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(await repo.GetPlaceAncestors(userId.Value, id, token));
    }

    static async Task<Results<Ok<SearchResult<Media>>, NotFound, BadRequest<string>>> GetPlaceMedia(
        ClaimsPrincipal user,
        IPlaceRepository repo,
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

        var result = await repo.GetPlaceMedia(userId.Value, request.GetBaseUrl(), id, o, MEDIA_LIMIT, f, seed, token);

        // 404 rather than an empty first page, and 404 rather than 403 for a place
        // the caller may see nothing at - the same rule the person media view
        // follows, so a caller cannot probe for places the listing hid from them.
        // an empty page past the first is a normal answer, not a missing place.
        //
        // a favourites filter is exempt: "nothing here is a favourite of yours" is
        // a real answer about a place you can see, and answering 404 would make an
        // empty filter look like a broken link.  seed is not exempt - it reorders
        // rather than filters, so an empty first page still means there is nothing
        // to show.
        return o == 0 && !f && !result.Results.Any()
            ? TypedResults.NotFound()
            : TypedResults.Ok(result);
    }

    static async Task<Results<Ok<SearchResult<Category>>, NotFound, BadRequest<string>>> GetPlaceCategories(
        ClaimsPrincipal user,
        IPlaceRepository repo,
        HttpRequest request,
        [FromRoute] Guid id,
        CancellationToken token,
        [FromQuery] int o = 0,
        [FromQuery] bool f = false
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

        var result = await repo.GetPlaceCategories(userId.Value, request.GetBaseUrl(), id, o, MEDIA_LIMIT, f, token);

        // the media view's 404 rule, and for the same reason, including the
        // favourites exemption
        return o == 0 && !f && !result.Results.Any()
            ? TypedResults.NotFound()
            : TypedResults.Ok(result);
    }

    static async Task<Results<Ok<Place>, NotFound, BadRequest<string>, ForbidHttpResult>> SetPlaceCover(
        ClaimsPrincipal user,
        IPlaceRepository repo,
        HttpRequest request,
        [FromRoute] Guid id,
        [FromBody] PlaceCoverRequest coverRequest,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var outcome = await repo.SetPlaceCover(userId.Value, id, coverRequest.MediaId, token);

        return outcome switch
        {
            // Forbid rather than 404 here, unlike the read side.  the caller has
            // already proved they hold the location administration scope, so
            // "you are not an admin" tells them nothing they could not learn by
            // trying any other admin route.
            PlaceCoverOutcome.NotAdmin => TypedResults.Forbid(),
            PlaceCoverOutcome.PlaceNotFound => TypedResults.NotFound(),
            PlaceCoverOutcome.MediaNotAtPlace =>
                TypedResults.BadRequest("That media is not at this place, or is not one you can see."),
            PlaceCoverOutcome.NoPublishableRendition =>
                TypedResults.BadRequest("That media has no publishable rendition. Originals are never published."),
            _ => await Reread(repo, userId.Value, request, id, token)
        };
    }

    static async Task<Results<Ok<Place>, NotFound, BadRequest<string>, ForbidHttpResult>> ClearPlaceCover(
        ClaimsPrincipal user,
        IPlaceRepository repo,
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

        var outcome = await repo.ClearPlaceCover(userId.Value, id, token);

        return outcome == PlaceCoverOutcome.NotAdmin
            ? TypedResults.Forbid()
            : await Reread(repo, userId.Value, request, id, token);
    }

    // the place is read back so a caller sees the cover url it just created
    // without composing it, and so clearing returns a place with no cover rather
    // than an empty body an admin screen has to interpret
    static async Task<Results<Ok<Place>, NotFound, BadRequest<string>, ForbidHttpResult>> Reread(
        IPlaceRepository repo,
        Guid userId,
        HttpRequest request,
        Guid id,
        CancellationToken token
    )
    {
        var place = await repo.GetPlace(userId, request.GetBaseUrl(), id, token);

        return place == null
            ? TypedResults.NotFound()
            : TypedResults.Ok(place);
    }

    static async Task<Results<Ok<Place>, NotFound, BadRequest<string>, ForbidHttpResult>> MergePlaces(
        ClaimsPrincipal user,
        IPlaceRepository repo,
        HttpRequest request,
        [FromRoute] Guid id,
        [FromBody] PlaceMergeRequest mergeRequest,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var outcome = await repo.MergePlaces(userId.Value, id, mergeRequest.SourceId, token);

        return Interpret(outcome) ?? await Reread(repo, userId.Value, request, id, token);
    }

    static async Task<Results<Ok<Place>, NotFound, BadRequest<string>, ForbidHttpResult>> SetPlaceParent(
        ClaimsPrincipal user,
        IPlaceRepository repo,
        HttpRequest request,
        [FromRoute] Guid id,
        [FromBody] PlaceParentRequest parentRequest,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var outcome = await repo.SetPlaceParent(userId.Value, id, parentRequest.ParentId, token);

        return Interpret(outcome) ?? await Reread(repo, userId.Value, request, id, token);
    }

    // null means the operation succeeded and the caller should read the place back.
    // the refusals are shared because merge and re-parent fail in overlapping ways
    // and an admin screen renders them identically.
    static Results<Ok<Place>, NotFound, BadRequest<string>, ForbidHttpResult>? Interpret(PlaceAdminOutcome outcome) =>
        outcome switch
        {
            PlaceAdminOutcome.Ok => null,
            // Forbid rather than 404, as on the cover routes: the caller already
            // proved they hold the location administration scope, so being told
            // they are not an admin reveals nothing new
            PlaceAdminOutcome.NotAdmin => TypedResults.Forbid(),
            PlaceAdminOutcome.NotFound => TypedResults.NotFound(),
            PlaceAdminOutcome.SamePlace =>
                TypedResults.BadRequest("A place cannot be merged into itself."),
            PlaceAdminOutcome.KindMismatch =>
                TypedResults.BadRequest("Both places must be the same kind - a state cannot be merged into a country."),
            PlaceAdminOutcome.InvalidParent =>
                TypedResults.BadRequest("That parent does not sit above this place in the hierarchy."),
            PlaceAdminOutcome.NotARootKind =>
                TypedResults.BadRequest("Only a country may sit at the root of the tree."),
            _ => TypedResults.BadRequest("The place could not be changed.")
        };
}

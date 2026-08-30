using System.Security.Claims;
using MawMedia.Authorization.Claims;
using MawMedia.Routes.Extensions;
using MawMedia.Models;
using MawMedia.Services.Abstractions;
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
        return TypedResults.Ok(await repo.GetPlaces(userId.Value, parent, kind, token));
    }

    static async Task<Results<Ok<Place>, NotFound>> GetPlace(
        ClaimsPrincipal user,
        IPlaceRepository repo,
        [FromRoute] Guid id,
        CancellationToken token
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        var place = await repo.GetPlace(userId.Value, id, token);

        // 404 rather than 403 for a place the caller may see nothing at, matching
        // the person read side - a 403 would confirm the place exists
        return place == null
            ? TypedResults.NotFound()
            : TypedResults.Ok(place);
    }

    static async Task<Results<Ok<IEnumerable<PlaceAncestor>>, NotFound>> GetPlaceAncestors(
        ClaimsPrincipal user,
        IPlaceRepository repo,
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
        if (await repo.GetPlace(userId.Value, id, token) == null)
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
}

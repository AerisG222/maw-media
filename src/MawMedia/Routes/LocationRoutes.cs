using System.Security.Claims;
using MawMedia.Authorization.Claims;
using MawMedia.Models;
using MawMedia.Services.Abstractions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace MawMedia.Routes;

public static class LocationRoutes
{
    public static RouteGroupBuilder MapLocationRoutes(this RouteGroupBuilder group)
    {
        group
            .MapGet("/missing-metadata", GetLocationsWithoutMetadata)
            .WithName("location-missing-metadata")
            .WithSummary("Missing Metadata")
            .WithDescription("Identifies locations that do not have reverse geocode data")
            .RequireAuthorization(AuthorizationPolicies.LocationReader);

        group
            .MapPost("/{id}/metadata", UpdateMetadata)
            .WithName("location-update-metadata")
            .WithSummary("Update Metadata")
            .WithDescription("Updates location reverse geocode data")
            .RequireAuthorization(AuthorizationPolicies.LocationWriter);

        return group;
    }

    static async Task<Results<Ok<IEnumerable<Location>>, ForbidHttpResult>> GetLocationsWithoutMetadata(
        ClaimsPrincipal user,
        ILocationRepository repo
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await repo.GetLocationsWithoutMetadata(userId.Value))
            : TypedResults.Ok(Array.Empty<Location>().AsEnumerable());
    }

    static async Task<Results<Ok<bool>, BadRequest, NotFound, ForbidHttpResult>> UpdateMetadata(
        ClaimsPrincipal user,
        ILocationRepository repo,
        [FromRoute] Guid id,
        [FromBody] LocationMetadata metadata
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return TypedResults.NotFound();
        }

        if(id != metadata.LocationId)
        {
            return TypedResults.BadRequest();
        }

        // todo: consider returning location metadata
        return TypedResults.Ok(await repo.SetLocationMetadata(userId.Value, metadata));
    }
}

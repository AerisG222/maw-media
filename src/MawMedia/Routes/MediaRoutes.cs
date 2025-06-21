using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MawMedia.Models;
using MawMedia.Services;

namespace MawMedia.Routes;

public static class MediaRoutes
{
    static readonly Guid DUMMYUSER = Guid.Parse("01977b3a-6db0-7384-87ad-8e56aad783ef");

    public static RouteGroupBuilder MapMediaRoutes(this RouteGroupBuilder group)
    {
        group.MapGet("/random/{count}", GetRandomMedia)
        .WithName("random-media")
        .WithSummary("Random Media")
        .WithDescription("Lists random media");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        return group;
    }

    static async Task<Results<Ok<IEnumerable<Media>>, ForbidHttpResult>> GetRandomMedia(IMediaRepository repo, HttpRequest request, [FromRoute] byte count) =>
        TypedResults.Ok(await repo.GetRandomMedia(DUMMYUSER, count));
}

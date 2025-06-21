using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MawMedia.Models;
using MawMedia.Services;
using MawMedia.ViewModels;

namespace MawMedia.Routes;

public static class MediaRoutes
{
    static readonly Guid DUMMYUSER = Guid.Parse("01977b3a-6db0-7384-87ad-8e56aad783ef");

    public static RouteGroupBuilder MapMediaRoutes(this RouteGroupBuilder group)
    {
        group
            .MapGet("/random/{count}", GetRandomMedia)
            .WithName("random-media")
            .WithSummary("Random Media")
            .WithDescription("Lists random media");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        group
            .MapGet("/{id}", GetMedia)
            .WithName("media")
            .WithSummary("Get Media")
            .WithDescription("Get media");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        group
            .MapPost("/{id}/favorite", FavoriteMedia)
            .WithName("favorite-media")
            .WithSummary("Favorite Media")
            .WithDescription("Favorites media");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        group
            .MapGet("/{id}/comments", GetComments)
            .WithName("media-comments")
            .WithSummary("Get Media Comments")
            .WithDescription("Get media comments");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        group
            .MapPost("/{id}/comments", AddComment)
            .WithName("add-media-comment")
            .WithSummary("Add Media Comment")
            .WithDescription("Add comment for media");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        return group;
    }

    static async Task<Results<Ok<IEnumerable<Media>>, ForbidHttpResult>> GetRandomMedia(IMediaRepository repo, HttpRequest request, [FromRoute] byte count) =>
        TypedResults.Ok(await repo.GetRandomMedia(DUMMYUSER, count));

    static async Task<Results<Ok<Media>, NotFound, ForbidHttpResult>> GetMedia(IMediaRepository repo, [FromRoute] Guid id)
    {
        var media = await repo.GetMedia(DUMMYUSER, id);

        if (media == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(media);
    }

    static async Task<Results<Ok<Media>, NotFound, ForbidHttpResult>> FavoriteMedia(IMediaRepository repo, [FromRoute] Guid id, [FromBody] FavoriteRequest request)
    {
        var media = await repo.SetIsFavorite(DUMMYUSER, id, request.IsFavorite);

        if (media == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(media);
    }

    static async Task<Results<Ok<IEnumerable<Comment>>, ForbidHttpResult>> GetComments(IMediaRepository repo, [FromRoute] Guid id) =>
        TypedResults.Ok(await repo.GetComments(DUMMYUSER, id));

    static async Task<Results<Ok<string>, NotFound, ForbidHttpResult>> AddComment(IMediaRepository repo, [FromRoute] Guid id, [FromBody] AddCommentRequest request)
    {
        var commentId = await repo.AddComment(DUMMYUSER, id, request.Body);

        if (commentId == null)
        {
            return TypedResults.NotFound();
        }

        // TODO: why can't we return guid?
        return TypedResults.Ok(commentId.ToString());
    }
}

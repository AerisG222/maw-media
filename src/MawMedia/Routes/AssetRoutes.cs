using Microsoft.AspNetCore.Http.HttpResults;
using MawMedia.Services;

namespace MawMedia.Routes;

public static class AssetRoutes
{
    static readonly Guid DUMMYUSER = Guid.Parse("01977b3a-6db0-7384-87ad-8e56aad783ef");

    public static RouteGroupBuilder MapAssetRoutes(this RouteGroupBuilder group)
    {
        group
            .MapGet("/{*path}", GetAsset)
            .WithName("Asset")
            .WithSummary("Get Asset")
            .WithDescription("Download asset");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        return group;
    }

    static async Task<Results<Ok<string>, ForbidHttpResult>> GetAsset(IMediaRepository repo, string path)
    {
        await Task.Delay(1);

        return TypedResults.Ok(path);
    }
}

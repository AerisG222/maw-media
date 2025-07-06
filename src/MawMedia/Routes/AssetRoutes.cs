using MawMedia.Services;

namespace MawMedia.Routes;

public static class AssetRoutes
{
    static readonly Guid DUMMYUSER = Guid.Parse("0197dd28-4980-7237-9c54-c515e74e4c03");

    public static RouteGroupBuilder MapAssetRoutes(this RouteGroupBuilder group)
    {
        group
            .MapGet("/{id}", GetAsset)
            .WithName("Asset")
            .WithSummary("Get Asset")
            .WithDescription("Download asset");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        return group;
    }

    static async Task<IResult> GetAsset(IMediaRepository repo, Guid id)
    {
        var file = await repo.GetMediaFile(DUMMYUSER, id);

        if (file == null)
        {
            return Results.NotFound();
        }

        //return Results.File(file.Path);

        return Results.Ok(file.Path);
    }
}

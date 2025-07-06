using MawMedia.Services;

namespace MawMedia.Routes;

public static class AssetRoutes
{
    static readonly Guid DUMMYUSER = Guid.Parse("0197e02e-7c7f-7c1e-bb77-ee35921e4c51");

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

    static async Task<IResult> GetAsset(IMediaRepository repo, string path)
    {
        var file = await repo.GetMediaFile(DUMMYUSER, Path.Combine("/assets", path));

        if (file == null)
        {
            return Results.NotFound();
        }

        //return Results.File(file.Path);

        return Results.Ok(file.Path);
    }
}

using MawMedia.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace MawMedia.Authorization;

public class MediaStaticAssetAuthorizationHandler
    : AuthorizationHandler<MediaStaticAssetRequirement>
{
    static readonly Guid DUMMYUSER = Guid.Parse("01997368-32db-7af5-83c3-00712e2304fd");

    readonly IMediaRepository _repo;

    public MediaStaticAssetAuthorizationHandler(IMediaRepository repo)
    {
        ArgumentNullException.ThrowIfNull(repo);

        _repo = repo;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MediaStaticAssetRequirement requirement
    )
    {
        var ctx = context.Resource as HttpContext;

        if (ctx == null)
        {
            return;
        }

        if (!ctx.Request.Path.StartsWithSegments(Constants.AssetBaseUrl))
        {
            return;
        }

        // ctx.User.Identity.Name
        if (await _repo.AllowAccessToAsset(DUMMYUSER, ctx.Request.Path, default))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }
    }
}

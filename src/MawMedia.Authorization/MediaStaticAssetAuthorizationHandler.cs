using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using MawMedia.Services;

namespace MawMedia.Authorization;

public class MediaStaticAssetAuthorizationHandler
    : AuthorizationHandler<MediaStaticAssetRequirement>
{
    static readonly Guid DUMMYUSER = Guid.Parse("0197e02e-7c7f-7c1e-bb77-ee35921e4c51");

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
        if (await _repo.HasAccess(DUMMYUSER, ctx.Request.Path, default))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }
    }
}

using MawMedia.Authorization.Claims;
using MawMedia.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Constants = MawMedia.Services.Abstractions.Constants;

namespace MawMedia.Authorization;

public class MediaStaticAssetAuthorizationHandler
    : AuthorizationHandler<MediaStaticAssetRequirement>
{
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

        var userId = ctx.User.GetMediaUserId();

        if (userId == null)
        {
            context.Fail();
        }
        else
        {
            if (await _repo.AllowAccessToAsset(userId.Value, ctx.Request.Path, default))
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }
        }
    }
}

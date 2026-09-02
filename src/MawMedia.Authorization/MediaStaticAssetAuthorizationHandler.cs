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
        if (context.Resource is not HttpContext ctx)
        {
            return;
        }

        if (!ctx.Request.Path.StartsWithSegments(Constants.AssetBaseUrl))
        {
            return;
        }

        // face crops sit under the same prefix but are governed by
        // FaceStaticAssetAuthorizationHandler.  declining here keeps the media
        // rule from being asked a question about a file it knows nothing about.
        if (ctx.Request.Path.StartsWithSegments(Constants.FaceAssetBaseUrl))
        {
            return;
        }

        // place covers likewise.  they are authorized by
        // AuthorizationPolicies.PlaceCoverStaticAsset, which deliberately performs
        // no per file lookup - asking this rule about one would reimpose exactly
        // the check a cover exists to skip.
        if (ctx.Request.Path.StartsWithSegments(Constants.PlaceCoverBaseUrl))
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
            if (await _repo.AllowAccessToAsset(userId.Value, ctx.Request.Path, ctx.RequestAborted))
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

using System.Security.Claims;
using MawMedia.Authorization.Claims;
using MawMedia.Models;
using MawMedia.Services.Abstractions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace MawMedia.Routes;

public static class UploadRoutes
{
    public static RouteGroupBuilder MapUploadRoutes(this RouteGroupBuilder group)
    {
        group
            .MapGet("/", GetFiles)
            .WithName("get-uploaded-files")
            .WithSummary("Uploaded Files")
            .WithDescription("Lists uploaded files")
            .RequireAuthorization(AuthorizationPolicies.MediaWriter);

        group
            .MapGet("/{filename}", DownloadFile)
            .WithName("download-uploaded-file")
            .WithSummary("Download File")
            .WithDescription("Downlad file that was previously uploaded")
            .RequireAuthorization(AuthorizationPolicies.MediaWriter);

        group
            .MapPost("/", UploadFile)
            .WithName("upload-file")
            .WithSummary("Upload File")
            .WithDescription("Downlad file that was previously uploaded")
            .DisableAntiforgery()
            .RequireAuthorization(AuthorizationPolicies.MediaWriter);

        return group;
    }

    static async Task<Results<Ok<IEnumerable<UploadedFile>>, ForbidHttpResult>> GetFiles(
        IUploadService svc,
        ClaimsPrincipal user,
        HttpRequest request
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await svc.GetFiles(userId.Value))
            : TypedResults.Ok(Array.Empty<UploadedFile>().AsEnumerable());
    }

    static async Task<Results<Ok<UploadedFile>, NotFound, ForbidHttpResult>> UploadFile(
        IUploadService svc,
        ClaimsPrincipal user,
        IFormFile file
    )
    {
        var userId = user.GetMediaUserId();

        return userId != null
            ? TypedResults.Ok(await svc.UploadFile(userId.Value, file.OpenReadStream(), file.FileName))
            : TypedResults.NotFound();
    }

    static async Task<IResult> DownloadFile(
        IUploadService svc,
        ClaimsPrincipal user,
        [FromRoute] string filename
    )
    {
        var userId = user.GetMediaUserId();

        if (userId == null)
        {
            return Results.NotFound();
        }

        var physicalFile = await svc.GetPhysicalFilePath(userId.Value, filename);

        return physicalFile != null
            ? Results.File(physicalFile)
            : Results.NotFound();
    }
}

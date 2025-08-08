using Microsoft.AspNetCore.Http.HttpResults;
using MawMedia.Models;
using MawMedia.Services;

namespace MawMedia.Routes;

public static class UploadRoutes
{
    static readonly Guid DUMMYUSER = Guid.Parse("0197e02e-7c7f-7c1e-bb77-ee35921e4c51");

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

    static async Task<Results<Ok<IEnumerable<UploadedFile>>, ForbidHttpResult>> GetFiles(IUploadService svc, HttpRequest request) =>
        TypedResults.Ok(await svc.GetFiles(DUMMYUSER));

    static async Task<Results<Ok<UploadedFile>, ForbidHttpResult>> UploadFile(IUploadService svc, IFormFile file) =>
        TypedResults.Ok(await svc.UploadFile(DUMMYUSER, file.OpenReadStream(), file.FileName));

    static async Task<IResult> DownloadFile(IUploadService svc, string filename)
    {
        var physicalFile = await svc.GetPhysicalFilePath(DUMMYUSER, filename);

        if (physicalFile == null)
        {
            return Results.NotFound();
        }

        return Results.File(physicalFile);
    }
}

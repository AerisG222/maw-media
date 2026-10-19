using System.Reflection;
using System.Security.Claims;
using MawMedia.Authorization;
using MawMedia.Authorization.Claims;
using MawMedia.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace MawMedia.Services.Tests;

// what the handler hands the repository, checked without a database.
//
// the path is the whole question here.  it is matched against media.file.path,
// which stores names exactly as published - spaces included - and against cache
// keys primed from those same stored paths.  a path that arrives in any other
// form matches neither, and the file is refused even to a caller allowed to see
// it.  that is not hypothetical: 43 renditions in the library have a space in
// their name, and every one of them was refused.
public class MediaStaticAssetAuthorizationHandlerTests
{
    [Theory]
    [InlineData("/assets/2017/201712/qvg/A-hunting We Will Go_ Lemieux.mp4")]
    [InlineData("/assets/2022/nature/full-hd/nature1.avif")]
    public async Task AsksAboutThePathAsStored(string path)
    {
        var repo = DispatchProxy.Create<IMediaRepository, RecordingRepository>();
        var recording = (RecordingRepository)(object)repo;
        var handler = new MediaStaticAssetAuthorizationHandler(repo);

        // the handler reads the caller from HttpContext.User, not from the
        // authorization context, so both carry it
        var caller = Caller();
        var http = new DefaultHttpContext { User = caller };
        http.Request.Path = new PathString(path);

        var requirement = new MediaStaticAssetRequirement();
        var context = new AuthorizationHandlerContext([requirement], caller, http);

        await handler.HandleAsync(context);

        // unescaped, as stored.  PathString's implicit conversion to string is its
        // escaped uri form - "A-hunting%20We..." - which no stored path is
        Assert.Equal(path, recording.AskedAbout);
        Assert.True(context.HasSucceeded);
    }

    // signed in, with the identity MediaIdentityClaimsTransformation attaches
    static ClaimsPrincipal Caller() =>
        new([
            new ClaimsIdentity([new Claim("sub", "test")], authenticationType: "test"),
            MediaIdentity.Create(Guid.CreateVersion7(), "active", false)
        ]);

    // stands in for the repository and records the one call this handler makes.
    // anything else is a test bug, so it throws rather than answering
    public class RecordingRepository
        : DispatchProxy
    {
        public string? AskedAbout { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IMediaRepository.AllowAccessToAsset))
            {
                AskedAbout = (string?)args?[1];

                return ValueTask.FromResult(true);
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}

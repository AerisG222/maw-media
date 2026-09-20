using System.Net;
using MawMedia;

namespace MawMedia.Services.Tests.Api;

// the caching half of the asset contract, and browser only in the same way
// CorsTests is: a missing Cache-Control changes no status code and no body, so
// every other test in this project passes while a photo grid revalidates all
// fifty of its thumbnails on every page load.
//
// what is asserted here is the *shape* of the policy rather than its exact
// duration - private, and bounded - because the duration is a judgement call
// that should be free to change without breaking a test.  the one exception is
// the cover branch, where immutable is load bearing rather than tuning: a cover
// is replaced by writing a new url, and nothing else in this api may claim that.
public class StaticAssetCacheTests
    : ApiTestBase
{
    public StaticAssetCacheTests(TestFixture fixture)
        : base(fixture)
    {

    }

    [Fact]
    public async Task MediaAssetIsCacheableAndPrivate()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var response = await client.GetAsync(
            new Uri(Constants.FILE_NATURE_1.Path, UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cacheControl = response.Headers.CacheControl;

        Assert.NotNull(cacheControl);

        // private, because the response was authorized for this caller alone and a
        // shared cache must not hand it to anybody else
        Assert.True(cacheControl.Private);
        Assert.False(cacheControl.Public);

        // bounded rather than immutable: a media file keeps its path when it is
        // re-rendered, so a cache that never revalidates could pin a stale
        // rendition forever
        Assert.NotNull(cacheControl.MaxAge);
        Assert.True(cacheControl.MaxAge > TimeSpan.Zero);
    }

    [Fact]
    public async Task MediaAssetStillAnswersAConditionalRequest()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;
        var path = new Uri(Constants.FILE_NATURE_1.Path, UriKind.Relative);

        var first = await client.GetAsync(path, token);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.NotNull(first.Headers.ETag);

        using var conditional = new HttpRequestMessage(HttpMethod.Get, path);
        conditional.Headers.IfNoneMatch.Add(first.Headers.ETag);

        var second = await client.SendAsync(conditional, token);

        // the cache header must not have cost us revalidation: a client that does
        // ask is still answered without the bytes
        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task CachingDoesNotWeakenTheAccessRule()
    {
        using var anonymous = Factory.CreateClient();

        var response = await anonymous.GetAsync(
            new Uri(Constants.FILE_NATURE_1.Path, UriKind.Relative),
            TestContext.Current.CancellationToken);

        // the header is applied by the static file middleware, which sits behind
        // the authorization step - an unauthenticated caller never reaches it
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.CacheControl);
    }
}

using System.Net;
using System.Net.Http.Json;
using Dapper;
using MawMedia;

namespace MawMedia.Services.Tests.Api;

// the route half of clearing an override.  the rules themselves - who may clear,
// what an absent override means, what happens to the location row - are proven in
// GpsOverrideClearTests; this file proves the http contract wired on top of them:
// the verbs, the scope, and that a refusal answers 404 the way setting one does.
//
// subjects come from GpsOverrideClearTests.CreateMedia for the reason that file
// gives - the seeded override fixture is read by the whole place suite.
public class MediaGpsOverrideRoutesTests
    : ApiTestBase
{
    readonly TestFixture _fixture;

    public MediaGpsOverrideRoutesTests(TestFixture fixture)
        : base(fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DeleteClearsTheOverride()
    {
        var token = TestContext.Current.CancellationToken;
        var scratch = await GpsOverrideClearTests.CreateMedia(_fixture, Constants.USER_ADMIN, 1);
        var media = scratch.Media[0];

        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaWrite);

        var response = await client.DeleteAsync($"/api/v1/media/{media.Id}/gps", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await OverrideOf(media.Id));
    }

    [Fact]
    public async Task DeleteByANonOwnerIsNotFound()
    {
        var token = TestContext.Current.CancellationToken;
        var scratch = await GpsOverrideClearTests.CreateMedia(_fixture, Constants.USER_ADMIN, 1);
        var media = scratch.Media[0];

        using var client = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.MediaWrite);

        var response = await client.DeleteAsync($"/api/v1/media/{media.Id}/gps", token);

        // 404 rather than 403, matching the PUT: a refusal must not confirm that
        // the media exists to a caller who cannot change it
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(media.OverrideLocationId, await OverrideOf(media.Id));
    }

    [Fact]
    public async Task DeleteRequiresTheWriteScope()
    {
        var token = TestContext.Current.CancellationToken;
        var scratch = await GpsOverrideClearTests.CreateMedia(_fixture, Constants.USER_ADMIN, 1);
        var media = scratch.Media[0];

        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var response = await client.DeleteAsync($"/api/v1/media/{media.Id}/gps", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(media.OverrideLocationId, await OverrideOf(media.Id));
    }

    [Fact]
    public async Task BulkClearClearsEveryOverride()
    {
        var token = TestContext.Current.CancellationToken;
        var scratch = await GpsOverrideClearTests.CreateMedia(_fixture, Constants.USER_ADMIN, 2);

        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaWrite);

        var response = await client.PostAsJsonAsync(
            "/api/v1/media/bulk-gps-override/clear",
            new { mediaIds = scratch.Media.Select(m => m.Id).ToArray() },
            JsonOptions,
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        foreach (var media in scratch.Media)
        {
            Assert.Null(await OverrideOf(media.Id));
        }
    }

    [Fact]
    public async Task BulkClearByANonAdminIsNotFound()
    {
        var token = TestContext.Current.CancellationToken;
        var scratch = await GpsOverrideClearTests.CreateMedia(_fixture, Constants.USER_ADMIN, 2);

        using var client = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.MediaWrite);

        var response = await client.PostAsJsonAsync(
            "/api/v1/media/bulk-gps-override/clear",
            new { mediaIds = scratch.Media.Select(m => m.Id).ToArray() },
            JsonOptions,
            token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        foreach (var media in scratch.Media)
        {
            Assert.Equal(media.OverrideLocationId, await OverrideOf(media.Id));
        }
    }

    async Task<Guid?> OverrideOf(Guid mediaId)
    {
        await using var conn = _fixture.DataSource.CreateConnection();

        return await conn.ExecuteScalarAsync<Guid?>(
            "SELECT location_override_id FROM media.media WHERE id = @mediaId",
            new { mediaId });
    }
}

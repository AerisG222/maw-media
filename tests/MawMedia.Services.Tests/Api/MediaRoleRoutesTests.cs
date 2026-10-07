using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MawMedia;
using MawMedia.Models;

namespace MawMedia.Services.Tests.Api;

// the http contract over restrictions: the verbs, the scope, and how each refusal
// answers.  the rules themselves are MediaRoleAdminTests' and MediaRestrictionTests'
// job; subjects come from RestrictionUniverse, signed in through the external
// identities it creates for its admin and friend.
public class MediaRoleRoutesTests
    : ApiTestBase
{
    readonly TestFixture _fixture;

    public MediaRoleRoutesTests(TestFixture fixture)
        : base(fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PutRestrictsAndAnswersWithTheStoredRoles()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        using var admin = Client(u.AdminExternalId, ApiScopes.MediaWrite);

        var put = await admin.PutAsJsonAsync(Roles(u.Spare.Id), new { roles = new[] { u.AdminRole, u.AdminRole } }, JsonOptions, token);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        // read back as stored - the duplicate folded away
        Assert.Equal([u.AdminRole], await ReadRoles(put, token));

        var get = await admin.GetAsync(Roles(u.Spare.Id), token);

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal([u.AdminRole], await ReadRoles(get, token));
    }

    [Fact]
    public async Task PutWithProblemsListsEveryOneOfThem()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        using var admin = Client(u.AdminExternalId, ApiScopes.MediaWrite);

        // the teaser, restricted to a role the category never grants
        var response = await admin.PutAsJsonAsync(Roles(u.Open.Id), new { roles = new[] { u.OutsiderRole } }, JsonOptions, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problems = await response.Content.ReadFromJsonAsync<MediaRestrictionProblem[]>(JsonOptions, token);

        Assert.NotNull(problems);
        Assert.Contains(problems, p => p.MediaId == u.Open.Id && p.Reason == "teaser");
        Assert.Contains(problems, p => p.MediaId == u.Open.Id && p.Reason == "role_not_granted" && p.Detail == u.OutsiderRole);
    }

    [Fact]
    public async Task PutForAMissingMediaIsNotFound()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        using var admin = Client(u.AdminExternalId, ApiScopes.MediaWrite);

        var response = await admin.PutAsJsonAsync(Roles(Guid.CreateVersion7()), new { roles = new[] { u.AdminRole } }, JsonOptions, token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteLiftsTheRestriction()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        using var admin = Client(u.AdminExternalId, ApiScopes.MediaWrite);

        var delete = await admin.DeleteAsync(Roles(u.AdminOnly.Id), token);

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Empty(await ReadRoles(await admin.GetAsync(Roles(u.AdminOnly.Id), token), token));
    }

    [Fact]
    public async Task BulkSetAndClear()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        using var admin = Client(u.AdminExternalId, ApiScopes.MediaWrite);

        var set = await admin.PostAsJsonAsync(
            "/api/v1/media/bulk-roles",
            new { mediaIds = new[] { u.Spare.Id }, roles = new[] { u.FriendRole } },
            JsonOptions,
            token);

        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        Assert.Equal([u.FriendRole], await ReadRoles(await admin.GetAsync(Roles(u.Spare.Id), token), token));

        var clear = await admin.PostAsJsonAsync(
            "/api/v1/media/bulk-roles/clear",
            new { mediaIds = new[] { u.Spare.Id, u.AdminOnly.Id } },
            JsonOptions,
            token);

        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        Assert.Empty(await ReadRoles(await admin.GetAsync(Roles(u.Spare.Id), token), token));
        Assert.Empty(await ReadRoles(await admin.GetAsync(Roles(u.AdminOnly.Id), token), token));
    }

    [Fact]
    public async Task BulkSetWithProblemsChangesNothing()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        using var admin = Client(u.AdminExternalId, ApiScopes.MediaWrite);

        var missing = Guid.CreateVersion7();
        var response = await admin.PostAsJsonAsync(
            "/api/v1/media/bulk-roles",
            new { mediaIds = new[] { u.Spare.Id, u.Covered.Id, missing }, roles = new[] { u.AdminRole } },
            JsonOptions,
            token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problems = await response.Content.ReadFromJsonAsync<MediaRestrictionProblem[]>(JsonOptions, token);

        // a missing id is one of the problems here, not a 404 - the request exists
        Assert.NotNull(problems);
        Assert.Contains(problems, p => p.MediaId == u.Covered.Id && p.Reason == "place_cover");
        Assert.Contains(problems, p => p.MediaId == missing && p.Reason == "not_found");
        Assert.Empty(await ReadRoles(await admin.GetAsync(Roles(u.Spare.Id), token), token));
    }

    [Fact]
    public async Task ANonAdminIsForbiddenEverywhere()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        using var friend = Client(u.FriendExternalId, ApiScopes.MediaWrite);

        Assert.Equal(HttpStatusCode.Forbidden, (await friend.GetAsync("/api/v1/roles", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await friend.GetAsync(Roles(u.AdminOnly.Id), token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await friend.PutAsJsonAsync(Roles(u.Spare.Id), new { roles = new[] { u.FriendRole } }, JsonOptions, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await friend.DeleteAsync(Roles(u.AdminOnly.Id), token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await friend.PostAsJsonAsync("/api/v1/media/bulk-roles/clear", new { mediaIds = new[] { u.AdminOnly.Id } }, JsonOptions, token)).StatusCode);
    }

    [Fact]
    public async Task ChangingRestrictionsRequiresTheWriteScope()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        using var admin = Client(u.AdminExternalId, ApiScopes.MediaRead);

        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PutAsJsonAsync(Roles(u.Spare.Id), new { roles = new[] { u.AdminRole } }, JsonOptions, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/roles", token)).StatusCode);
    }

    [Fact]
    public async Task AnAdminCanListRoles()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        using var admin = Client(u.AdminExternalId, ApiScopes.MediaWrite);

        var response = await admin.GetAsync("/api/v1/roles", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var roles = await response.Content.ReadFromJsonAsync<string[]>(JsonOptions, token);

        Assert.NotNull(roles);
        Assert.Contains(u.AdminRole, roles);
        Assert.Contains("admin", roles);
    }

    [Fact]
    public async Task TheRestrictedListingIsServedToAnAdminOnly()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        using var admin = Client(u.AdminExternalId, ApiScopes.MediaWrite);
        using var friend = Client(u.FriendExternalId, ApiScopes.MediaWrite);

        var response = await admin.GetAsync($"/api/v1/media/restricted?categoryId={u.Category}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var restricted = await response.Content.ReadFromJsonAsync<RestrictedMedia[]>(JsonOptions, token);

        Assert.NotNull(restricted);
        Assert.Contains(restricted, r => r.MediaId == u.AdminOnly.Id && r.IsVisibleToYou);
        // OutsiderOnly is restricted to a role the admin does not hold
        Assert.Contains(restricted, r => r.MediaId == u.OutsiderOnly.Id && !r.IsVisibleToYou);

        Assert.Equal(HttpStatusCode.Forbidden, (await friend.GetAsync("/api/v1/media/restricted", token)).StatusCode);
    }

    [Fact]
    public async Task ACategorysRestrictionsAreServedToAnAdminOnly()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        using var admin = Client(u.AdminExternalId, ApiScopes.MediaWrite);
        using var friend = Client(u.FriendExternalId, ApiScopes.MediaWrite);

        var response = await admin.GetAsync($"/api/v1/categories/{u.Category}/restrictions", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var restricted = await response.Content.ReadFromJsonAsync<RestrictedMedia[]>(JsonOptions, token);

        // only the restricted media, the way the gps call lists only media with
        // gps - a client joins this to the category's media on mediaId
        Assert.NotNull(restricted);
        Assert.Equal(
            new[] { u.AdminOnly.Id, u.OutsiderOnly.Id }.Order(),
            restricted.Select(r => r.MediaId).Order());
        Assert.True(Assert.Single(restricted, r => r.MediaId == u.AdminOnly.Id).IsVisibleToYou);
        Assert.False(Assert.Single(restricted, r => r.MediaId == u.OutsiderOnly.Id).IsVisibleToYou);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await friend.GetAsync($"/api/v1/categories/{u.Category}/restrictions", token)).StatusCode);
    }

    [Fact]
    public async Task MediaPayloadsSayNothingAboutRestrictions()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        // deliberately, for admins too: restrictions are reported by their own
        // call, so the media every client reads carries no admin only concept
        foreach (var externalId in new[] { u.AdminExternalId, u.FriendExternalId })
        {
            using var client = Client(externalId, ApiScopes.MediaRead);
            using var media = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/categories/{u.Category}/media", token));

            Assert.NotEmpty(media.RootElement.EnumerateArray());
            Assert.All(media.RootElement.EnumerateArray(), m =>
                Assert.False(m.TryGetProperty("isRestricted", out _)));
        }
    }

    static string Roles(Guid mediaId) => $"/api/v1/media/{mediaId}/roles";

    static async Task<string[]> ReadRoles(HttpResponseMessage response, CancellationToken token)
    {
        var roles = await response.Content.ReadFromJsonAsync<string[]>(JsonOptions, token);

        Assert.NotNull(roles);

        return roles;
    }
}

using System.Net;
using System.Net.Http.Json;
using MawMedia;
using MawMedia.Models;

namespace MawMedia.Services.Tests.Api;

// the http contract over a category's roles: the verbs, the scope, and how each
// refusal answers.  the rules are CategoryRoleAdminTests'; subjects come from
// RestrictionUniverse, signed in through its admin and friend.
public class CategoryRoleRoutesTests
    : ApiTestBase
{
    readonly TestFixture _fixture;

    public CategoryRoleRoutesTests(TestFixture fixture)
        : base(fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PutReplacesTheRolesAndAnswersWithThemAsStored()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        using var admin = Client(u.AdminExternalId, ApiScopes.MediaWrite);

        var put = await admin.PutAsJsonAsync(
            Roles(u.Category),
            new { roles = new[] { u.OutsiderRole, u.AdminRole, u.FriendRole, u.AdminRole } },
            JsonOptions,
            token);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        // read back as stored: deduplicated, in name order
        string[] expected = [u.AdminRole, u.FriendRole, u.OutsiderRole];

        Assert.Equal(expected, await ReadRoles(put, token));
        Assert.Equal(expected, await ReadRoles(await admin.GetAsync(Roles(u.Category), token), token));
    }

    [Fact]
    public async Task PutWithProblemsListsThemAndChangesNothing()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        using var admin = Client(u.AdminExternalId, ApiScopes.MediaWrite);

        var response = await admin.PutAsJsonAsync(Roles(u.Category), new { roles = new[] { u.FriendRole } }, JsonOptions, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problems = await response.Content.ReadFromJsonAsync<CategoryRolesProblem[]>(JsonOptions, token);

        Assert.NotNull(problems);
        Assert.Contains(problems, p => p.MediaId == u.AdminOnly.Id && p.Reason == "restriction_depends" && p.Detail == u.AdminRole);
        Assert.Contains(problems, p => p.Reason == "would_hide_from_you");
        Assert.Equal([u.AdminRole, u.FriendRole], await ReadRoles(await admin.GetAsync(Roles(u.Category), token), token));
    }

    [Fact]
    public async Task AMissingCategoryIsNotFound()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        using var admin = Client(u.AdminExternalId, ApiScopes.MediaWrite);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(Roles(Guid.CreateVersion7()), token)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await admin.PutAsJsonAsync(Roles(Guid.CreateVersion7()), new { roles = new[] { u.AdminRole } }, JsonOptions, token)).StatusCode);
    }

    [Fact]
    public async Task ANonAdminIsForbidden()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        using var friend = Client(u.FriendExternalId, ApiScopes.MediaWrite);

        Assert.Equal(HttpStatusCode.Forbidden, (await friend.GetAsync(Roles(u.Category), token)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await friend.PutAsJsonAsync(Roles(u.Category), new { roles = new[] { u.FriendRole } }, JsonOptions, token)).StatusCode);
    }

    [Fact]
    public async Task ChangingRolesRequiresTheWriteScope()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        using var admin = Client(u.AdminExternalId, ApiScopes.MediaRead);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await admin.PutAsJsonAsync(Roles(u.Category), new { roles = new[] { u.AdminRole } }, JsonOptions, token)).StatusCode);
    }

    static string Roles(Guid categoryId) => $"/api/v1/categories/{categoryId}/roles";

    static async Task<string[]> ReadRoles(HttpResponseMessage response, CancellationToken token)
    {
        var roles = await response.Content.ReadFromJsonAsync<string[]>(JsonOptions, token);

        Assert.NotNull(roles);

        return roles;
    }
}

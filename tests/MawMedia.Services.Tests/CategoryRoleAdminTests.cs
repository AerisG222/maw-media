using Dapper;
using MawMedia.Services.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using NodaTime;

namespace MawMedia.Services.Tests;

// changing who may see a category - media.set_category_roles - through the
// repository.  every subject comes from RestrictionUniverse: its category is
// granted to the private admin and friend roles, not to outsider, and holds a
// media restricted to admin and one restricted to outsider.
public class CategoryRoleAdminTests
{
    const string BASE_URL = "https://example.test";

    readonly TestFixture _fixture;

    public CategoryRoleAdminTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    [Fact]
    public async Task AnAdminReadsTheRoles()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        var result = await Repo().GetCategoryRoles(u.Admin, u.Category, token);

        Assert.Equal(CategoryRolesOutcome.Ok, result.Outcome);
        Assert.Equal([u.AdminRole, u.FriendRole], result.Roles);
    }

    [Fact]
    public async Task GrantingARoleOpensTheCategoryToIt()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        Assert.Null(await Media().GetMedia(u.Outsider, BASE_URL, u.Open.Id, token));

        var result = await Repo().SetCategoryRoles(u.Admin, u.Category, [u.AdminRole, u.FriendRole, u.OutsiderRole], token);

        Assert.Equal(CategoryRolesOutcome.Ok, result.Outcome);
        Assert.NotNull(await Media().GetMedia(u.Outsider, BASE_URL, u.Open.Id, token));

        // and the restriction to outsider, which granted nothing while the
        // category did not, now grants that photo to them - and only to them
        Assert.NotNull(await Media().GetMedia(u.Outsider, BASE_URL, u.OutsiderOnly.Id, token));
        Assert.Null(await Media().GetMedia(u.Friend, BASE_URL, u.OutsiderOnly.Id, token));
    }

    [Fact]
    public async Task RemovingARoleClosesTheCategoryToIt()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        var result = await Repo().SetCategoryRoles(u.Admin, u.Category, [u.AdminRole], token);

        Assert.Equal(CategoryRolesOutcome.Ok, result.Outcome);
        Assert.Null(await Media().GetMedia(u.Friend, BASE_URL, u.Open.Id, token));
        Assert.NotNull(await Media().GetMedia(u.Admin, BASE_URL, u.Open.Id, token));
    }

    [Fact]
    public async Task ANewlyGrantedUserReceivesTheCategoryInIncrementalSync()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        // as if the category was last changed an hour ago, and the outsider's
        // client last synced half an hour ago
        await using (var conn = _fixture.SetupDataSource.CreateConnection())
        {
            await conn.ExecuteAsync(
                "UPDATE media.category SET modified = NOW() - INTERVAL '1 hour' WHERE id = @id",
                new { id = u.Category });
        }

        var lastSync = SystemClock.Instance.GetCurrentInstant() - Duration.FromMinutes(30);

        await Repo().SetCategoryRoles(u.Admin, u.Category, [u.AdminRole, u.FriendRole, u.OutsiderRole], token);

        // without the modified bump the category would predate the client's last
        // sync, and a user newly granted it would never be sent it
        var updates = await Repo().GetCategoryUpdates(u.Outsider, lastSync, BASE_URL, token);

        Assert.Contains(updates, c => c.Id == u.Category);
    }

    [Fact]
    public async Task EveryProblemIsReportedAndNothingChanges()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        // dropping admin, which AdminOnly's restriction lists, and handing the
        // category to a role the admin making the change does not hold
        var result = await Repo().SetCategoryRoles(u.Admin, u.Category, [u.FriendRole, "no-such-role"], token);

        Assert.Equal(CategoryRolesOutcome.Invalid, result.Outcome);

        var problems = result.Problems.Select(p => (p.MediaId, p.Reason, p.Detail)).ToList();

        Assert.Contains((null, "unknown_role", "no-such-role"), problems);
        Assert.Contains((u.AdminOnly.Id, "restriction_depends", u.AdminRole), problems);

        Assert.Equal([u.AdminRole, u.FriendRole], (await Repo().GetCategoryRoles(u.Admin, u.Category, token)).Roles);
    }

    [Fact]
    public async Task AChangeThatWouldHideTheCategoryFromYouIsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        // clear the restriction that depends on admin first, so the only thing
        // left to object to is the admin hiding the category from themselves
        await Media().ClearMediaRoles(u.Admin, [u.AdminOnly.Id], token);

        var result = await Repo().SetCategoryRoles(u.Admin, u.Category, [u.FriendRole], token);

        Assert.Equal(CategoryRolesOutcome.Invalid, result.Outcome);
        Assert.Equal("would_hide_from_you", Assert.Single(result.Problems).Reason);
        Assert.NotNull(await Media().GetMedia(u.Admin, BASE_URL, u.Open.Id, token));
    }

    [Fact]
    public async Task AnEmptyListIsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        var result = await Repo().SetCategoryRoles(u.Admin, u.Category, [], token);

        Assert.Equal(CategoryRolesOutcome.Invalid, result.Outcome);
        Assert.Equal("no_roles", Assert.Single(result.Problems).Reason);
    }

    [Fact]
    public async Task OnlyAnAdminMayReadOrChangeThem()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        Assert.Equal(CategoryRolesOutcome.NotAdmin, (await Repo().GetCategoryRoles(u.Friend, u.Category, token)).Outcome);
        Assert.Equal(CategoryRolesOutcome.NotAdmin, (await Repo().SetCategoryRoles(u.Friend, u.Category, [u.FriendRole], token)).Outcome);
        Assert.Equal([u.AdminRole, u.FriendRole], (await Repo().GetCategoryRoles(u.Admin, u.Category, token)).Roles);
    }

    [Fact]
    public async Task AMissingCategoryIsNotFound()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        Assert.Equal(CategoryRolesOutcome.NotFound, (await Repo().GetCategoryRoles(u.Admin, Guid.CreateVersion7(), token)).Outcome);
        Assert.Equal(CategoryRolesOutcome.NotFound, (await Repo().SetCategoryRoles(u.Admin, Guid.CreateVersion7(), [u.AdminRole], token)).Outcome);
    }

    CategoryRepository Repo() =>
        new(new FakeLogger<CategoryRepository>(), _fixture.DataSource.CreateConnection(), new FakeHybridCache(), new AssetPathBuilder());

    MediaRepository Media() =>
        new(new FakeLogger<MediaRepository>(), _fixture.DataSource.CreateConnection(), new FakeHybridCache(), new AssetPathBuilder());
}

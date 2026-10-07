using MawMedia.Services.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace MawMedia.Services.Tests;

// changing restrictions - media.set_media_roles and its neighbours - through the
// repository.  what a restriction *does* once set is MediaRestrictionTests' job;
// this file is about who may set one, what is refused, and that a refusal changes
// nothing.  every subject comes from RestrictionUniverse, for the reason it gives.
public class MediaRoleAdminTests
{
    const string BASE_URL = "https://example.test";

    readonly TestFixture _fixture;

    public MediaRoleAdminTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    [Fact]
    public async Task AnAdminRestrictsAMediaAndTheRuleTakesEffect()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        Assert.NotNull(await Repo().GetMedia(u.Friend, BASE_URL, u.Spare.Id, token));

        var result = await Repo().SetMediaRoles(u.Admin, [u.Spare.Id], [u.AdminRole], token);

        Assert.Equal(MediaRestrictionOutcome.Ok, result.Outcome);
        Assert.Null(await Repo().GetMedia(u.Friend, BASE_URL, u.Spare.Id, token));
        Assert.NotNull(await Repo().GetMedia(u.Admin, BASE_URL, u.Spare.Id, token));
        Assert.Equal([u.AdminRole], (await Repo().GetMediaRoles(u.Admin, u.Spare.Id, token)).Roles);
    }

    [Fact]
    public async Task SettingARestrictionReplacesTheOneThere()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        await Repo().SetMediaRoles(u.Admin, [u.Spare.Id], [u.AdminRole], token);
        await Repo().SetMediaRoles(u.Admin, [u.Spare.Id], [u.FriendRole], token);

        // the whole list, not a delta: admin is gone, and the private admin - who
        // holds neither friend nor anything else this category grants - loses it
        Assert.Equal([u.FriendRole], (await Repo().GetMediaRoles(u.Admin, u.Spare.Id, token)).Roles);
        Assert.NotNull(await Repo().GetMedia(u.Friend, BASE_URL, u.Spare.Id, token));
        Assert.Null(await Repo().GetMedia(u.Admin, BASE_URL, u.Spare.Id, token));
    }

    [Fact]
    public async Task EveryProblemIsReportedAndNothingChanges()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);
        var missing = Guid.CreateVersion7();

        var result = await Repo().SetMediaRoles(
            u.Admin,
            [u.Spare.Id, u.Open.Id, u.Covered.Id, missing],
            [u.AdminRole, "no-such-role", u.OutsiderRole],
            token);

        Assert.Equal(MediaRestrictionOutcome.Invalid, result.Outcome);

        var problems = result.Problems.Select(p => (p.MediaId, p.Reason, p.Detail)).ToList();

        Assert.Contains((null, "unknown_role", "no-such-role"), problems);
        Assert.Contains((missing, "not_found", null), problems);
        // outsider is not granted the category, so it could never see these
        Assert.Contains((u.Spare.Id, "role_not_granted", u.OutsiderRole), problems);
        Assert.Contains((u.Open.Id, "role_not_granted", u.OutsiderRole), problems);
        // the teaser and the cover are each shown to a wider audience than the
        // restriction would allow
        Assert.Contains(problems, p => p.MediaId == u.Open.Id && p.Reason == "teaser");
        Assert.Contains(problems, p => p.MediaId == u.Covered.Id && p.Reason == "place_cover");

        // and Spare, which on its own would have passed the admin role, was not
        // restricted either: all or nothing
        Assert.Empty((await Repo().GetMediaRoles(u.Admin, u.Spare.Id, token)).Roles);
    }

    [Fact]
    public async Task AnEmptyRoleListIsRefusedRatherThanHidingTheMedia()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        var result = await Repo().SetMediaRoles(u.Admin, [u.Spare.Id], [], token);

        Assert.Equal(MediaRestrictionOutcome.Invalid, result.Outcome);
        Assert.Equal("no_roles", Assert.Single(result.Problems).Reason);
        Assert.NotNull(await Repo().GetMedia(u.Friend, BASE_URL, u.Spare.Id, token));
    }

    [Fact]
    public async Task ClearingLiftsTheRestriction()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        Assert.Null(await Repo().GetMedia(u.Friend, BASE_URL, u.AdminOnly.Id, token));

        Assert.True(await Repo().ClearMediaRoles(u.Admin, [u.AdminOnly.Id], token));

        Assert.NotNull(await Repo().GetMedia(u.Friend, BASE_URL, u.AdminOnly.Id, token));
        Assert.Empty((await Repo().GetMediaRoles(u.Admin, u.AdminOnly.Id, token)).Roles);
    }

    [Fact]
    public async Task OnlyAnAdminMayReadOrChangeRestrictions()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        Assert.Null(await Repo().GetRoles(u.Friend, token));
        Assert.Equal(MediaRestrictionOutcome.NotAdmin, (await Repo().GetMediaRoles(u.Friend, u.AdminOnly.Id, token)).Outcome);
        Assert.Equal(MediaRestrictionOutcome.NotAdmin, (await Repo().SetMediaRoles(u.Friend, [u.Spare.Id], [u.FriendRole], token)).Outcome);
        Assert.False(await Repo().ClearMediaRoles(u.Friend, [u.AdminOnly.Id], token));

        // and none of it took effect
        Assert.Empty((await Repo().GetMediaRoles(u.Admin, u.Spare.Id, token)).Roles);
        Assert.Equal([u.AdminRole], (await Repo().GetMediaRoles(u.Admin, u.AdminOnly.Id, token)).Roles);
    }

    [Fact]
    public async Task AnAdminCanListTheRoles()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        var roles = await Repo().GetRoles(u.Admin, token);

        Assert.NotNull(roles);
        Assert.Contains("admin", roles);
        Assert.Contains(u.AdminRole, roles);
        Assert.Contains(u.FriendRole, roles);
    }

    [Fact]
    public async Task AskingAboutAMissingMediaIsNotFound()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        Assert.Equal(MediaRestrictionOutcome.NotFound, (await Repo().GetMediaRoles(u.Admin, Guid.CreateVersion7(), token)).Outcome);
    }

    [Fact]
    public async Task AnAdminCanFindAPhotoTheyHaveHiddenFromThemselves()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        // restricted to friend, which the admin does not hold - so it leaves every
        // listing the admin can make
        await Repo().SetMediaRoles(u.Admin, [u.Spare.Id], [u.FriendRole], token);

        Assert.DoesNotContain(
            await Categories().GetCategoryMedia(u.Admin, BASE_URL, u.Category, token),
            m => m.Id == u.Spare.Id);

        var restricted = await Repo().GetRestrictedMedia(u.Admin, u.Category, token);

        Assert.NotNull(restricted);

        var spare = Assert.Single(restricted, r => r.MediaId == u.Spare.Id);

        Assert.False(spare.IsVisibleToYou);
        Assert.Equal([u.FriendRole], spare.Roles);
        Assert.True(Assert.Single(restricted, r => r.MediaId == u.AdminOnly.Id).IsVisibleToYou);

        // and the restriction can then be lifted, which brings it back
        await Repo().ClearMediaRoles(u.Admin, [u.Spare.Id], token);

        Assert.Contains(
            await Categories().GetCategoryMedia(u.Admin, BASE_URL, u.Category, token),
            m => m.Id == u.Spare.Id);
    }

    [Fact]
    public async Task OnlyAnAdminCanListRestrictedMedia()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        Assert.Null(await Repo().GetRestrictedMedia(u.Friend, u.Category, token));
        Assert.Null(await Repo().GetRestrictedMedia(u.Both, null, token));
    }

    MediaRepository Repo() =>
        new(new FakeLogger<MediaRepository>(), _fixture.DataSource.CreateConnection(), new FakeHybridCache(), new AssetPathBuilder());

    CategoryRepository Categories() =>
        new(new FakeLogger<CategoryRepository>(), _fixture.DataSource.CreateConnection(), new FakeHybridCache(), new AssetPathBuilder());
}

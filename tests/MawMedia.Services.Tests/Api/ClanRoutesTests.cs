using System.Net;
using System.Net.Http.Json;
using MawMedia;
using MawMedia.Models;
using MawMedia.Models.FaceRecognition;
using MawMedia.Routes;
using MawMedia.ViewModels;

namespace MawMedia.Services.Tests.Api;

public class ClanRoutesTests
    : ApiTestBase
{
    const string ROUTE = "/api/v1/clans";

    public ClanRoutesTests(TestFixture fixture)
        : base(fixture)
    {

    }

    [Fact]
    public async Task ClansRequireTheFaceRecognitionReadScope()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var response = await client.GetAsync(ROUTE, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ClansRequireAuthentication()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync(ROUTE, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AClanRoundTripsThroughCreateReadUpdateAndDelete()
    {
        using var client = Reader();
        var token = TestContext.Current.CancellationToken;
        var name = UniqueName();

        var created = await client.PostAsJsonAsync(
            ROUTE, new ClanRequest(name, [Constants.PERSON_SHARED]), JsonOptions, token);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var clan = await created.Content.ReadFromJsonAsync<Clan>(JsonOptions, token);

        Assert.Equal(name, clan!.Name);
        Assert.Equal(Constants.PERSON_SHARED, Assert.Single(clan.Members).Id);

        // Location points at the new clan so a client can follow it
        Assert.EndsWith($"{ROUTE}/{clan.Id}", created.Headers.Location?.ToString());

        try
        {
            var fetched = await client.GetFromJsonAsync<Clan>($"{ROUTE}/{clan.Id}", JsonOptions, token);

            Assert.Equal(clan.Id, fetched!.Id);

            var renamed = UniqueName();

            var updated = await client.PutAsJsonAsync(
                $"{ROUTE}/{clan.Id}", new ClanRequest(renamed, [Constants.PERSON_PRIVATE]), JsonOptions, token);

            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

            var afterUpdate = await updated.Content.ReadFromJsonAsync<Clan>(JsonOptions, token);

            Assert.Equal(renamed, afterUpdate!.Name);
            Assert.Equal(Constants.PERSON_PRIVATE, Assert.Single(afterUpdate.Members).Id);

            // members can also be replaced on their own, without resending a name
            var members = await client.PutAsJsonAsync(
                $"{ROUTE}/{clan.Id}/persons",
                new ClanPersonsRequest([Constants.PERSON_SHARED, Constants.PERSON_PRIVATE]),
                JsonOptions, token);

            Assert.Equal(HttpStatusCode.OK, members.StatusCode);

            var afterMembers = await members.Content.ReadFromJsonAsync<Clan>(JsonOptions, token);

            Assert.Equal(2, afterMembers!.Members.Count());
            Assert.Equal(renamed, afterMembers.Name);
        }
        finally
        {
            var deleted = await client.DeleteAsync($"{ROUTE}/{clan.Id}", token);

            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        var gone = await client.GetAsync($"{ROUTE}/{clan.Id}", token);

        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task ARenameLeavesMembershipAloneWhenNoPeopleAreSent()
    {
        using var client = Reader();
        var token = TestContext.Current.CancellationToken;

        var clan = await Create(client, [Constants.PERSON_SHARED], token);

        try
        {
            // a null member list means "leave it alone", not "empty it" - a client
            // editing only the name should not have to resend everyone
            var updated = await client.PutAsJsonAsync(
                $"{ROUTE}/{clan.Id}", new ClanRequest(UniqueName(), null), JsonOptions, token);

            var afterUpdate = await updated.Content.ReadFromJsonAsync<Clan>(JsonOptions, token);

            Assert.Equal(Constants.PERSON_SHARED, Assert.Single(afterUpdate!.Members).Id);
        }
        finally
        {
            await client.DeleteAsync($"{ROUTE}/{clan.Id}", token);
        }
    }

    [Fact]
    public async Task AnUnusablePersonIsABadRequest()
    {
        using var client = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.FaceRecognitionRead);
        var token = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync(
            ROUTE, new ClanRequest(UniqueName(), [Constants.PERSON_PRIVATE]), JsonOptions, token);

        // the caller's mistake, not a missing clan
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ADuplicateNameIsAConflict()
    {
        using var client = Reader();
        var token = TestContext.Current.CancellationToken;

        var clan = await Create(client, [], token);

        try
        {
            var response = await client.PostAsJsonAsync(
                ROUTE, new ClanRequest(clan.Name, []), JsonOptions, token);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            await client.DeleteAsync($"{ROUTE}/{clan.Id}", token);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AClanNeedsAName(string name)
    {
        using var client = Reader();

        var response = await client.PostAsJsonAsync(
            ROUTE, new ClanRequest(name, []), JsonOptions, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TooManyPeopleIsRejected()
    {
        using var client = Reader();

        var tooMany = Enumerable
            .Range(0, ClanRoutes.MAX_CLAN_PERSONS + 1)
            .Select(_ => Guid.CreateVersion7())
            .ToArray();

        var response = await client.PostAsJsonAsync(
            ROUTE, new ClanRequest(UniqueName(), tooMany), JsonOptions, TestContext.Current.CancellationToken);

        // caught by the bound before the ids are ever looked up
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SomeoneElsesClanIsNotFound()
    {
        using var owner = Reader();
        using var other = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.FaceRecognitionRead);
        var token = TestContext.Current.CancellationToken;

        var clan = await Create(owner, [Constants.PERSON_SHARED], token);

        try
        {
            // 404 across the board rather than 403: a caller must not learn that
            // a clan id they guessed belongs to someone else
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{ROUTE}/{clan.Id}", token)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"{ROUTE}/{clan.Id}", token)).StatusCode);

            var update = await other.PutAsJsonAsync(
                $"{ROUTE}/{clan.Id}", new ClanRequest(UniqueName(), null), JsonOptions, token);

            Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

            var media = await other.GetAsync($"{ROUTE}/{clan.Id}/media", token);

            Assert.Equal(HttpStatusCode.NotFound, media.StatusCode);
        }
        finally
        {
            await owner.DeleteAsync($"{ROUTE}/{clan.Id}", token);
        }
    }

    [Fact]
    public async Task ClanMediaBehavesLikePersonMedia()
    {
        using var client = Reader();
        var token = TestContext.Current.CancellationToken;

        var clan = await Create(client, [Constants.PERSON_SHARED, Constants.PERSON_PRIVATE], token);

        try
        {
            var all = await client.GetFromJsonAsync<SearchResult<Media>>(
                $"{ROUTE}/{clan.Id}/media", JsonOptions, token);

            Assert.Equal(
                [Constants.MEDIA_TRAVEL_1.Id, Constants.MEDIA_NATURE_1.Id],
                all!.Results.Select(m => m.Id).ToList()
            );

            var favorites = await client.GetFromJsonAsync<SearchResult<Media>>(
                $"{ROUTE}/{clan.Id}/media?f=true", JsonOptions, token);

            Assert.Equal(Constants.MEDIA_TRAVEL_1.Id, Assert.Single(favorites!.Results).Id);

            var shuffled = await client.GetFromJsonAsync<SearchResult<Media>>(
                $"{ROUTE}/{clan.Id}/media?seed=7", JsonOptions, token);

            Assert.Equal(2, shuffled!.Results.Count());

            var negative = await client.GetAsync($"{ROUTE}/{clan.Id}/media?o=-1", token);

            Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        }
        finally
        {
            await client.DeleteAsync($"{ROUTE}/{clan.Id}", token);
        }
    }

    [Fact]
    public async Task AnEmptyClanHasNoMedia()
    {
        using var client = Reader();
        var token = TestContext.Current.CancellationToken;

        var clan = await Create(client, [], token);

        try
        {
            var response = await client.GetAsync($"{ROUTE}/{clan.Id}/media", token);

            // the clan itself is still readable, which is how a client tells an
            // empty clan apart from a missing one
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{ROUTE}/{clan.Id}", token)).StatusCode);
        }
        finally
        {
            await client.DeleteAsync($"{ROUTE}/{clan.Id}", token);
        }
    }

    [Fact]
    public async Task ListingReturnsTheCallersOwnClansOnly()
    {
        using var owner = Reader();
        using var other = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.FaceRecognitionRead);
        var token = TestContext.Current.CancellationToken;

        var clan = await Create(owner, [Constants.PERSON_SHARED], token);

        try
        {
            var mine = await owner.GetFromJsonAsync<Clan[]>(ROUTE, JsonOptions, token);
            var theirs = await other.GetFromJsonAsync<Clan[]>(ROUTE, JsonOptions, token);

            Assert.Contains(mine!, c => c.Id == clan.Id);
            Assert.DoesNotContain(theirs!, c => c.Id == clan.Id);
        }
        finally
        {
            await owner.DeleteAsync($"{ROUTE}/{clan.Id}", token);
        }
    }

    async Task<Clan> Create(HttpClient client, Guid[] personIds, CancellationToken token)
    {
        var response = await client.PostAsJsonAsync(
            ROUTE, new ClanRequest(UniqueName(), personIds), JsonOptions, token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<Clan>(JsonOptions, token))!;
    }

    HttpClient Reader() => Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);

    static string UniqueName() => $"clan-api-{Guid.CreateVersion7()}";
}

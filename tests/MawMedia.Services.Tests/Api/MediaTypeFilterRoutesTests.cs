using System.Net;
using System.Net.Http.Json;
using MawMedia.Models;

namespace MawMedia.Services.Tests.Api;

// the query string around the filters: what they accept, and that filtering down
// to nothing is an answer rather than a missing person.  what they return is in
// MediaTypeFilterTests.
public class MediaTypeFilterRoutesTests
    : ApiTestBase
{
    public MediaTypeFilterRoutesTests(TestFixture fixture)
        : base(fixture)
    {

    }

    static string PersonMedia => $"/api/v1/persons/{Constants.PERSON_SHARED}/media";
    static string PersonCategories => $"/api/v1/persons/{Constants.PERSON_SHARED}/categories";

    [Theory]
    [InlineData("audio")]
    [InlineData("PHOTO")]
    [InlineData("video-poster")]
    public async Task AnUnknownTypeIsRefused(string type)
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);
        using var reader = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        foreach (var route in new[] { PersonMedia, PersonCategories })
        {
            var response = await client.GetAsync($"{route}?t={type}", token);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var search = await reader.GetAsync($"/api/v1/categories/search?s=nature&t={type}", token);

        Assert.Equal(HttpStatusCode.BadRequest, search.StatusCode);
    }

    // every seeded media is a photo.  a 404 here would look like the person had gone
    [Fact]
    public async Task FilteringToNothingIsAnEmptyPageNotAMissingPerson()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);
        var token = TestContext.Current.CancellationToken;

        var media = await client.GetFromJsonAsync<SearchResult<Media>>($"{PersonMedia}?t=video", JsonOptions, token);
        var categories = await client.GetFromJsonAsync<SearchResult<Category>>($"{PersonCategories}?t=video", JsonOptions, token);

        Assert.Empty(media!.Results);
        Assert.Empty(categories!.Results);
    }

    [Fact]
    public async Task ThePhotoFilterKeepsThePhotos()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);

        var media = await client.GetFromJsonAsync<SearchResult<Media>>(
            $"{PersonMedia}?t=photo", JsonOptions, TestContext.Current.CancellationToken);

        Assert.NotEmpty(media!.Results);
    }
}

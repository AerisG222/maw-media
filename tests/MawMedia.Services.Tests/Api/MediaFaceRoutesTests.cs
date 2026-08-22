using System.Net;
using System.Net.Http.Json;
using MawMedia;
using MawMedia.Models.FaceRecognition;

namespace MawMedia.Services.Tests.Api;

public class MediaFaceRoutesTests
    : ApiTestBase
{
    public MediaFaceRoutesTests(TestFixture fixture)
        : base(fixture)
    {

    }

    [Fact]
    public async Task MediaFacesRequireTheFaceRecognitionReadScope()
    {
        // media:read gets the photo but not the overlay: the face feature is
        // gated on its own scope so it can be withdrawn from a client without
        // taking media access with it
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var response = await client.GetAsync(Route(Constants.MEDIA_NATURE_1.Id), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MediaFacesRequireAuthentication()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync(Route(Constants.MEDIA_NATURE_1.Id), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MediaFacesReturnBoxesForEveryFaceOnTheImage()
    {
        using var client = Reader();
        var token = TestContext.Current.CancellationToken;

        var faces = await client.GetFromJsonAsync<Face[]>(Route(Constants.MEDIA_NATURE_1.Id), JsonOptions, token);

        // asserted by identity rather than by count: FaceRepositoryTests syncs
        // faces against this same photo in parallel, so the total is not stable
        Assert.Contains(faces!, f => f.Id == Constants.FACE_PRIVATE_NATURE);

        var shared = Assert.Single(faces!, f => f.Id == Constants.FACE_SHARED_NATURE);

        Assert.Equal(Constants.PERSON_SHARED, shared.PersonId);
        Assert.Equal(0.1m, shared.BoxX);
        Assert.Equal(0.2m, shared.BoxY);
        Assert.Equal(0.3m, shared.BoxWidth);
        Assert.Equal(0.4m, shared.BoxHeight);
    }

    [Fact]
    public async Task MediaFacesAreEmptyForMediaTheCallerCannotSee()
    {
        using var client = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.FaceRecognitionRead);
        var token = TestContext.Current.CancellationToken;

        var response = await client.GetAsync(Route(Constants.MEDIA_NATURE_1.Id), token);

        // 200 with nothing to draw rather than 404: a media item with no faces
        // and one the caller cannot reach are the same answer here, and the media
        // route itself already tells them apart
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var faces = await response.Content.ReadFromJsonAsync<Face[]>(JsonOptions, token);

        Assert.Empty(faces!);
    }

    [Fact]
    public async Task MediaFacesAreEmptyForAnUnknownMedia()
    {
        using var client = Reader();
        var token = TestContext.Current.CancellationToken;

        var faces = await client.GetFromJsonAsync<Face[]>(Route(Guid.CreateVersion7()), JsonOptions, token);

        Assert.Empty(faces!);
    }

    static string Route(Guid mediaId) => $"/api/v1/media/{mediaId}/faces";

    HttpClient Reader() => Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);
}

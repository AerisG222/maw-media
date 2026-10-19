using System.Net;
using System.Net.Http.Json;
using MawMedia;

namespace MawMedia.Services.Tests.Api;

// what the write routes refuse outright, and that they refuse it as the caller's
// mistake - a 4xx naming the problem - rather than storing it or failing with a
// 500.
public class InputLimitTests
    : ApiTestBase
{
    public InputLimitTests(TestFixture fixture)
        : base(fixture)
    {

    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyCommentIsRefused(string body)
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.CommentsWrite);

        var response = await client.PostAsJsonAsync(Comments(), new { body }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnOversizedCommentIsRefused()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.CommentsWrite);

        var response = await client.PostAsJsonAsync(Comments(), new { body = new string('x', 4_001) }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadingANameAlreadyTakenIsAConflict()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaWrite);
        var name = $"dupe-{Guid.CreateVersion7():N}.jpg";

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsync(Upload(), Form(name, [1, 2, 3]), token)).StatusCode);

        var again = await client.PostAsync(Upload(), Form(name, [4, 5, 6]), token);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // and the first upload is untouched
        Assert.Equal([1, 2, 3], await client.GetByteArrayAsync($"{Upload()}/{name}", token));
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("bad;name.jpg")]
    public async Task AnUnacceptableUploadNameIsRefused(string name)
    {
        var token = TestContext.Current.CancellationToken;
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaWrite);

        var response = await client.PostAsync(Upload(), Form(name, [1]), token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DownloadingAnUnacceptableNameIsNotFound()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaWrite);

        var response = await client.GetAsync($"{Upload()}/bad;name.jpg", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    static string Comments() => $"/api/v1/media/{Constants.MEDIA_NATURE_1.Id}/comments";

    static string Upload() => "/api/v1/upload";

    static MultipartFormDataContent Form(string name, byte[] bytes)
    {
        var form = new MultipartFormDataContent();

        // the filename set raw: HttpClient would otherwise quote and encode it, and
        // the point is to send exactly this name
        var file = new ByteArrayContent(bytes);
        file.Headers.Add("Content-Disposition", $"form-data; name=\"file\"; filename=\"{name}\"");
        form.Add(file);

        return form;
    }
}

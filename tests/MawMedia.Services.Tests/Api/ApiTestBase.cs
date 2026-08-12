using System.Text.Json;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace MawMedia.Services.Tests.Api;

public abstract class ApiTestBase
    : IDisposable
{
    // the api speaks camelCase like every other endpoint; NodaTime is needed for
    // PersonSync.SourceModified
    protected static readonly JsonSerializerOptions JsonOptions =
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

    protected ApiFactory Factory { get; }

    protected ApiTestBase(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        Factory = new ApiFactory(fixture.ConnectionString);
    }

    public void Dispose()
    {
        Factory.Dispose();

        GC.SuppressFinalize(this);
    }

    // sub resolves against the seeded media.external_identity rows, so the real
    // claims transformation attaches the media user id and admin flag
    protected HttpClient Client(string sub, string scope)
    {
        var client = Factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HEADER_SUB, sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.HEADER_SCOPES, ApiFactory.QualifiedScope(scope));

        return client;
    }
}

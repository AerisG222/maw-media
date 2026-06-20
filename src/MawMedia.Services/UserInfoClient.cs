using System.Text.Json;
using MawMedia.Models;
using MawMedia.Services.Abstractions;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace MawMedia.Services;

public class UserInfoClient
    : IUserInfoClient
{
    static readonly JsonSerializerOptions _jsonOpts = BuildJsonOptions();

    readonly HttpClient _client;

    public UserInfoClient(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
    }

    public async Task<UserInfo?> QueryUserInfo()
    {
        var json = await _client.GetStringAsync("userinfo");

        return JsonSerializer.Deserialize<UserInfo>(json, _jsonOpts);
    }

    static JsonSerializerOptions BuildJsonOptions()
    {
        var opts = new JsonSerializerOptions(JsonSerializerOptions.Web);
        opts.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

        return opts;
    }
}

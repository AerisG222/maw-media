using System.Text.Json;
using MawMedia.Models;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace MawMedia.Services;

public class UserInfoClient
    : IUserInfoClient
{
    readonly HttpClient _client;

    public UserInfoClient(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
    }

    public async Task<UserInfo?> QueryUserInfo()
    {
        // todo: is there an easy way to use just the line below
        // [currently is not honoring global json options config which registers nodatime]
        // await _client.GetFromJsonAsync<UserInfo?>("userinfo");

        var json = await _client.GetStringAsync("userinfo");

        var opts = new JsonSerializerOptions(JsonSerializerOptions.Web);
        opts.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

        return JsonSerializer.Deserialize<UserInfo>(json, opts);
    }
}

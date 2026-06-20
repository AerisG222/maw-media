using MawMedia.Models;
using MawMedia.Services.Abstractions;

namespace MawMedia.Services.Tests;

public class FakeUserInfoClient
    : IUserInfoClient
{
    public Task<UserInfo?> QueryUserInfo(CancellationToken token = default)
    {
        return Task.FromResult((UserInfo?)new UserInfo());
    }
}
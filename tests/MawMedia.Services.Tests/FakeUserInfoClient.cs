using MawMedia.Models;
using MawMedia.Services;

public class FakeUserInfoClient
    : IUserInfoClient
{
    public Task<UserInfo?> QueryUserInfo()
    {
        return Task.FromResult((UserInfo?)new UserInfo());
    }
}

using MawMedia.Models;

namespace MawMedia.Services.Abstractions;

public interface IUserInfoClient
{
    Task<UserInfo?> QueryUserInfo(CancellationToken token = default);
}

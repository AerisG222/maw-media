using MawMedia.Models;

namespace MawMedia.Services;

public interface IUserInfoClient
{
    Task<UserInfo?> QueryUserInfo();
}

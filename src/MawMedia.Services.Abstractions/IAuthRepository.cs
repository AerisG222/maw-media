using MawMedia.Models;

namespace MawMedia.Services;

public interface IAuthRepository
{
    Task<bool> GetIsAdmin(Guid userId);
    Task<IUserState> GetUserState(string externalId, CancellationToken token);
    Task<IUserState> OnboardExternalIdentity();
}

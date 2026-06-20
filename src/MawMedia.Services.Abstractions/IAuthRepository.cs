using MawMedia.Models;

namespace MawMedia.Services.Abstractions;

public interface IAuthRepository
{
    Task<bool> GetIsAdmin(Guid userId, CancellationToken token = default);
    Task<IUserState> GetUserState(string externalId, CancellationToken token = default);
    Task<IUserState> OnboardExternalIdentity(CancellationToken token = default);
}

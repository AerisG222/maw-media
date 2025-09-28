using MawMedia.Models;
using Microsoft.Extensions.Logging.Testing;

namespace MawMedia.Services.Tests;

public class AuthRepositoryTests
{
    readonly TestFixture _fixture;

    public AuthRepositoryTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    public static TheoryData<string, Type, Guid?, bool> GetUserStateData => new()
    {
        { Guid.CreateVersion7().ToString(),  typeof(NonExistentUser), null, false },
        { Constants.EXTERNAL_ID_NOUSER, typeof(NonActivatedUser), null, false },
        { Constants.EXTERNAL_ID_USERADMIN, typeof(ActivatedUser), Constants.USER_ADMIN, true },
        { Constants.EXTERNAL_ID_JOHNDOE, typeof(ActivatedUser), Constants.USER_JOHNDOE, false }
    };

    [Theory]
    [MemberData(nameof(GetUserStateData))]
    public async Task GetUserStatus(string externalId, Type expectedType, Guid? userId, bool IsAdmin)
    {
        var repo = GetRepo();

        var state = await repo.GetUserState(externalId, TestContext.Current.CancellationToken);

        Assert.Equal(expectedType, state.GetType());

        if (state is NonActivatedUser nonactive)
        {
            Assert.Equal(externalId, nonactive.ExternalId);
        }

        if (state is ActivatedUser active)
        {
            Assert.Equal(externalId, active.ExternalId);
            Assert.Equal(userId, active.UserId);
            Assert.Equal(IsAdmin, active.IsAdmin);
        }
    }

    public static TheoryData<Guid, bool> GetIsAdminData => new()
    {
        { Guid.CreateVersion7(), false },
        { Constants.USER_ADMIN, true },
        { Constants.USER_JOHNDOE, false }
    };

    [Theory]
    [MemberData(nameof(GetIsAdminData))]
    public async Task IsAdmin(Guid userId, bool isAdmin)
    {
        var repo = GetRepo();

        var result = await repo.GetIsAdmin(userId);

        Assert.Equal(isAdmin, result);
    }

    AuthRepository GetRepo()
    {
        return new AuthRepository(
            new FakeLogger<AuthRepository>(),
            _fixture.DataSource.CreateConnection(),
            new FakeHybridCache(),
            new FakeUserInfoClient()
        );
    }
}

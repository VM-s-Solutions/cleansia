using System.Security.Cryptography;
using System.Text;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;
using RefreshToken = Cleansia.Core.Domain.Users.RefreshToken;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// The trusted-device lockout bypass for the mobile login commands. Native clients can't read
/// the HttpOnly refresh cookie the web hosts use, so MobileLogin/MobilePartnerLogin carry the marker in
/// the request body. The shared LoginValidator reads it server-side exactly as it does for the web
/// commands, so the bypass semantics are byte-identical: a valid, non-revoked, account-bound token lets
/// the password rule run despite the lock; an absent token leaves the lock standing.
/// </summary>
public class MobileLoginTrustedDeviceBypassTests
{
    private const string Password = TestUtilities.Constants.TestUserSession.TestUserPassword;
    private const string RawTrustedToken = "trusted-device-refresh-token-raw-value";

    private static string Hash(string raw)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

    private static User LockedUser()
        => UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            Password = Password.HashAndSaltPassword(),
            LockoutEndsAt = DateTimeOffset.UtcNow.AddMinutes(10),
        });

    private static RefreshToken AliveTokenFor(string userId, string rawToken)
        => RefreshToken.Create(
            userId: userId,
            tokenHash: Hash(rawToken),
            expiresAt: DateTimeOffset.UtcNow.AddDays(7),
            audience: "audience",
            deviceLabel: null,
            ipAddress: null);

    private static Mock<IRefreshTokenService> TokenHasher()
    {
        var service = new Mock<IRefreshTokenService>();
        service.Setup(s => s.HashToken(It.IsAny<string>())).Returns<string>(Hash);
        return service;
    }

    private static (Mock<IUserRepository> users, Mock<IRefreshTokenRepository> tokens, Mock<IRefreshTokenService> hasher) Repos(
        User user, RefreshToken? tokenByHash = null)
    {
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.ExistsWithEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        users.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var tokens = new Mock<IRefreshTokenRepository>();
        tokens.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);
        if (tokenByHash is not null)
        {
            tokens.Setup(r => r.GetByTokenHashAsync(Hash(RawTrustedToken), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tokenByHash);
        }
        return (users, tokens, TokenHasher());
    }

    [Fact]
    public async Task MobileLogin_Locked_Account_With_A_Valid_Body_Token_Lets_The_Correct_Password_Pass()
    {
        var user = LockedUser();
        var (users, tokens, hasher) = Repos(user, AliveTokenFor(user.Id, RawTrustedToken));
        var validator = new MobileLogin.Validator(users.Object, tokens.Object, hasher.Object);

        var result = await validator.ValidateAsync(
            new MobileLogin.Command(user.Email, Password, true, RawTrustedToken));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task MobileLogin_Locked_Account_Without_A_Body_Token_Stays_Locked()
    {
        var user = LockedUser();
        var (users, tokens, hasher) = Repos(user);
        var validator = new MobileLogin.Validator(users.Object, tokens.Object, hasher.Object);

        var result = await validator.ValidateAsync(new MobileLogin.Command(user.Email, Password, true));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.AccountLocked);
        tokens.Verify(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MobilePartnerLogin_Locked_Account_With_A_Valid_Body_Token_Lets_The_Correct_Password_Pass()
    {
        var user = LockedUser();
        var (users, tokens, hasher) = Repos(user, AliveTokenFor(user.Id, RawTrustedToken));
        var validator = new MobilePartnerLogin.Validator(users.Object, tokens.Object, hasher.Object);

        var result = await validator.ValidateAsync(
            new MobilePartnerLogin.Command(user.Email, Password, true, RawTrustedToken));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task MobilePartnerLogin_Locked_Account_With_A_Token_Bound_To_Another_User_Stays_Locked()
    {
        var user = LockedUser();
        var (users, tokens, hasher) = Repos(user, AliveTokenFor("some-other-user-id", RawTrustedToken));
        var validator = new MobilePartnerLogin.Validator(users.Object, tokens.Object, hasher.Object);

        var result = await validator.ValidateAsync(
            new MobilePartnerLogin.Command(user.Email, Password, true, RawTrustedToken));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.AccountLocked);
    }
}

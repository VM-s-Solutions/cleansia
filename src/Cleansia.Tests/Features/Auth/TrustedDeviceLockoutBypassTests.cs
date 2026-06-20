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
/// T-0233 trusted-device lockout bypass. A device that still presents a valid, non-revoked,
/// non-expired refresh token bound to the SAME account may have the lockout gate bypassed so the
/// password is evaluated — restoring access for the common legit case while a distributed sprayer
/// without that artifact sees byte-identical T-0193 lock behavior. The marker only gates whether the
/// password check runs; it never grants a session (S1-S4).
/// </summary>
public class TrustedDeviceLockoutBypassTests
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
        users.Setup(r => r.ExistsWithEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        users.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

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
    public async Task A_Locked_Account_With_A_Valid_Account_Bound_Token_Lets_The_Correct_Password_Pass()
    {
        var user = LockedUser();
        var (users, tokens, hasher) = Repos(user, AliveTokenFor(user.Id, RawTrustedToken));
        var validator = new Login.Validator(users.Object, tokens.Object, hasher.Object);

        var result = await validator.ValidateAsync(
            new Login.Command(user.Email, Password, true) { TrustedDeviceToken = RawTrustedToken });

        Assert.True(result.IsValid);
        users.Verify(r => r.RecordFailedLoginAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Locked_Account_With_A_Valid_Token_But_Wrong_Password_Still_Fails_And_Charges_The_Counter()
    {
        var user = LockedUser();
        var (users, tokens, hasher) = Repos(user, AliveTokenFor(user.Id, RawTrustedToken));
        var validator = new Login.Validator(users.Object, tokens.Object, hasher.Object);

        var result = await validator.ValidateAsync(
            new Login.Command(user.Email, Password + "wrong", true) { TrustedDeviceToken = RawTrustedToken });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidPassword);
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.AccountLocked);
        users.Verify(r => r.RecordFailedLoginAsync(user.Email, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Locked_Account_With_A_Token_Bound_To_Another_User_Stays_Locked()
    {
        var user = LockedUser();
        var (users, tokens, hasher) = Repos(user, AliveTokenFor("some-other-user-id", RawTrustedToken));
        var validator = new Login.Validator(users.Object, tokens.Object, hasher.Object);

        var result = await validator.ValidateAsync(
            new Login.Command(user.Email, Password, true) { TrustedDeviceToken = RawTrustedToken });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.AccountLocked);
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidPassword);
        users.Verify(r => r.RecordFailedLoginAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Locked_Account_With_A_Revoked_Token_Stays_Locked()
    {
        var user = LockedUser();
        var revoked = AliveTokenFor(user.Id, RawTrustedToken).Revoke("logout", DateTimeOffset.UtcNow.AddMinutes(-1));
        var (users, tokens, hasher) = Repos(user, revoked);
        var validator = new Login.Validator(users.Object, tokens.Object, hasher.Object);

        var result = await validator.ValidateAsync(
            new Login.Command(user.Email, Password, true) { TrustedDeviceToken = RawTrustedToken });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.AccountLocked);
    }

    [Fact]
    public async Task A_Locked_Account_With_An_Expired_Token_Stays_Locked()
    {
        var user = LockedUser();
        var expired = RefreshToken.Create(
            userId: user.Id,
            tokenHash: Hash(RawTrustedToken),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1),
            audience: "audience",
            deviceLabel: null,
            ipAddress: null);
        var (users, tokens, hasher) = Repos(user, expired);
        var validator = new Login.Validator(users.Object, tokens.Object, hasher.Object);

        var result = await validator.ValidateAsync(
            new Login.Command(user.Email, Password, true) { TrustedDeviceToken = RawTrustedToken });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.AccountLocked);
    }

    [Fact]
    public async Task A_Locked_Account_With_No_Token_Stays_Locked_Exactly_Like_T0193()
    {
        var user = LockedUser();
        var (users, tokens, hasher) = Repos(user);
        var validator = new Login.Validator(users.Object, tokens.Object, hasher.Object);

        var result = await validator.ValidateAsync(new Login.Command(user.Email, Password, true));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.AccountLocked);
        users.Verify(r => r.RecordFailedLoginAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        tokens.Verify(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdminLogin_Honours_The_Trusted_Device_Bypass()
    {
        var user = LockedUser();
        var (users, tokens, hasher) = Repos(user, AliveTokenFor(user.Id, RawTrustedToken));
        var validator = new AdminLogin.Validator(users.Object, tokens.Object, hasher.Object);

        var result = await validator.ValidateAsync(
            new AdminLogin.Command(user.Email, Password, true) { TrustedDeviceToken = RawTrustedToken });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task AdminLogin_Without_A_Token_Stays_Locked()
    {
        var user = LockedUser();
        var (users, tokens, hasher) = Repos(user);
        var validator = new AdminLogin.Validator(users.Object, tokens.Object, hasher.Object);

        var result = await validator.ValidateAsync(new AdminLogin.Command(user.Email, Password, true));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.AccountLocked);
    }

    [Fact]
    public async Task PartnerLogin_Honours_The_Trusted_Device_Bypass()
    {
        var user = LockedUser();
        var (users, tokens, hasher) = Repos(user, AliveTokenFor(user.Id, RawTrustedToken));
        var validator = new PartnerLogin.Validator(users.Object, tokens.Object, hasher.Object);

        var result = await validator.ValidateAsync(
            new PartnerLogin.Command(user.Email, Password, true) { TrustedDeviceToken = RawTrustedToken });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task PartnerLogin_Without_A_Token_Stays_Locked()
    {
        var user = LockedUser();
        var (users, tokens, hasher) = Repos(user);
        var validator = new PartnerLogin.Validator(users.Object, tokens.Object, hasher.Object);

        var result = await validator.ValidateAsync(new PartnerLogin.Command(user.Email, Password, true));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.AccountLocked);
    }
}

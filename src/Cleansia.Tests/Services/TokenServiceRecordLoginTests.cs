using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;
using RefreshTokenEntity = Cleansia.Core.Domain.Users.RefreshToken;

namespace Cleansia.Tests.Services;

public class TokenServiceRecordLoginTests
{
    private const string Audience = JwtAudiences.Customer;
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly Mock<IJwtSettings> _jwtSettings = new();
    private readonly Mock<IRefreshTokenService> _refreshTokenService = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IRequestMetadataProvider> _requestMetadata = new();
    private readonly StubTimeProvider _timeProvider = new(Now);

    public TokenServiceRecordLoginTests()
    {
        _jwtSettings.SetupGet(s => s.Secret).Returns(new string('k', 64));
        _jwtSettings.SetupGet(s => s.Issuer).Returns("cleansia.tests");
        _jwtSettings.SetupGet(s => s.AccessTokenExpMinutes).Returns(15);

        _refreshTokenService
            .Setup(s => s.Issue(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns((string userId, bool _, string audience, string? __, string? ___, string? ____) =>
                new IssuedRefreshToken(
                    "raw",
                    RefreshTokenEntity.Create(userId, "hash", Now.AddDays(7), audience, null, null)));
    }

    private TokenService CreateSut() => new(
        _jwtSettings.Object,
        _refreshTokenService.Object,
        _employeeRepository.Object,
        _requestMetadata.Object,
        _timeProvider);

    [Fact]
    public async Task GenerateTokenAsync_On_Success_Records_Login_With_Clock_Now()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = UserProfile.Customer });
        Assert.Null(user.LastLoginAt);

        await CreateSut().GenerateTokenAsync(user, rememberMe: false, Audience);

        Assert.Equal(Now, user.LastLoginAt);
    }

    [Fact]
    public async Task GenerateTokenAsync_AccessTokenClock_RidesTheInjectedTimeProvider()
    {
        // T-0410: the whole access-token clock (nbf + exp) is derived from TimeProvider, so expiry
        // boundaries are unit-testable without real waits (and can't drift from NotBefore).
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = UserProfile.Customer });

        var response = await CreateSut().GenerateTokenAsync(user, rememberMe: false, Audience);

        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(response.Token);
        // NotBefore is deliberately backdated 60s to absorb inter-instance clock drift under ClockSkew=Zero
        // (ADR-0024). Crucially, exp is STILL exactly Now + TTL — the revocation bound TC-REVOKE-TTL-2 pins is
        // unchanged; only the not-before window widened.
        Assert.Equal(Now.UtcDateTime.AddSeconds(-60), jwt.ValidFrom);
        Assert.Equal(Now.UtcDateTime.AddMinutes(15), jwt.ValidTo);
    }

    [Fact]
    public async Task GenerateTokenAsync_When_Email_Unconfirmed_Does_Not_Record_Login()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = UserProfile.Customer });
        ForceUnconfirmed(user);
        Assert.False(user.IsEmailConfirmed);

        var response = await CreateSut().GenerateTokenAsync(user, rememberMe: false, Audience);

        Assert.False(response.IsEmailConfirmed);
        Assert.Null(user.LastLoginAt);
    }

    private static void ForceUnconfirmed(User user)
    {
        typeof(User)
            .GetProperty(nameof(User.IsEmailConfirmed))!
            .SetValue(user, false);
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

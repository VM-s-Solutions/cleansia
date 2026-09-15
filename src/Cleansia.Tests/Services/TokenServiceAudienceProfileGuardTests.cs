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

/// <summary>
/// Each host's login refuses the profiles its audience does not serve (<c>PartnerLogin</c>,
/// <c>MobilePartnerLogin</c>, <c>AdminLogin</c>, and the host gate in <c>GoogleAuth</c>), and every one of
/// those checks is per command. The mint itself is the one seam every issuing command crosses, so the
/// service refuses the same pairs as an invariant: a command that forgot its gate cannot hand a Customer
/// a partner-audience session, whichever route asked for it.
/// </summary>
public sealed class TokenServiceAudienceProfileGuardTests
{
    private readonly Mock<IJwtSettings> _jwtSettings = new();
    private readonly Mock<IRefreshTokenService> _refreshTokenService = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();

    public TokenServiceAudienceProfileGuardTests()
    {
        _jwtSettings.SetupGet(s => s.Secret).Returns(new string('k', 64));
        _jwtSettings.SetupGet(s => s.Issuer).Returns("cleansia.tests");
        _jwtSettings.SetupGet(s => s.AccessTokenExpMinutes).Returns(15);

        _refreshTokenService
            .Setup(s => s.Issue(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns((string userId, bool _, string audience, string? __, string? ___, string? ____) =>
                new IssuedRefreshToken(
                    "raw",
                    RefreshTokenEntity.Create(userId, "hash", DateTimeOffset.UtcNow.AddDays(7), audience, null, null)));
        _employeeRepository
            .Setup(r => r.GetByUserEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);
    }

    private TokenService Sut() => new(
        _jwtSettings.Object,
        _refreshTokenService.Object,
        _employeeRepository.Object,
        Mock.Of<IRequestMetadataProvider>(),
        Mock.Of<ITenantProvider>(),
        TimeProvider.System);

    private static User ConfirmedAccount(UserProfile profile) =>
        UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = profile });

    [Theory]
    [InlineData(JwtAudiences.Partner, UserProfile.Customer)]
    [InlineData(JwtAudiences.Mobile, UserProfile.Customer)]
    [InlineData(JwtAudiences.Admin, UserProfile.Customer)]
    [InlineData(JwtAudiences.Admin, UserProfile.Employee)]
    public async Task A_Profile_The_Audience_Does_Not_Serve_Is_Never_Minted_A_Session(string audience, UserProfile profile)
    {
        var user = ConfirmedAccount(profile);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Sut().GenerateTokenAsync(user, rememberMe: true, audience));

        _refreshTokenService.Verify(
            s => s.Issue(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);
        Assert.Null(user.LastLoginAt);
    }

    [Theory]
    [InlineData(JwtAudiences.Partner, UserProfile.Employee)]
    [InlineData(JwtAudiences.Partner, UserProfile.Administrator)]
    [InlineData(JwtAudiences.Mobile, UserProfile.Employee)]
    [InlineData(JwtAudiences.Mobile, UserProfile.Administrator)]
    [InlineData(JwtAudiences.Admin, UserProfile.Administrator)]
    [InlineData(JwtAudiences.Customer, UserProfile.Customer)]
    [InlineData(JwtAudiences.Customer, UserProfile.Employee)]
    [InlineData(JwtAudiences.Customer, UserProfile.Administrator)]
    public async Task A_Profile_The_Audience_Serves_Is_Minted_A_Session(string audience, UserProfile profile)
    {
        var user = ConfirmedAccount(profile);

        var response = await Sut().GenerateTokenAsync(user, rememberMe: true, audience);

        Assert.True(response.IsEmailConfirmed);
        Assert.False(string.IsNullOrEmpty(response.Token));
        Assert.Equal(profile.ToString(), response.Role);
    }

    // An audience no host mints for has no login to agree with, so the backstop fails closed on it.
    [Fact]
    public async Task An_Unknown_Audience_Is_Never_Minted_A_Session()
    {
        var user = ConfirmedAccount(UserProfile.Administrator);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Sut().GenerateTokenAsync(user, rememberMe: true, "cleansia.functions"));
    }
}

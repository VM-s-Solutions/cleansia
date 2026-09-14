using System.IdentityModel.Tokens.Jwt;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;
using RefreshTokenEntity = Cleansia.Core.Domain.Users.RefreshToken;

namespace Cleansia.Tests.Services;

/// <summary>
/// ADR-0061 D4 — every token mint runs on an anonymous request, so the RefreshToken row it adds would
/// be stamped with no tenant (and the column is NOT NULL). The request adopts the tenant of the user
/// being authenticated BEFORE the row is added — which is also what the JWT it returns says — and it
/// does so even when the scope behaviour set a market's operator first (social sign-in of an existing
/// account of another company): the later override wins. The adoption is the authentication's, not the
/// mint's: a correct password on an unconfirmed address opens no session, and the sign-in row it still
/// leaves (ADR-0062 D7) must carry the account's operator, not the default market's.
/// </summary>
public sealed class TokenServiceAdoptsUserTenantTests
{
    private const string Audience = "cleansia.customer";

    private readonly Mock<IJwtSettings> _jwtSettings = new();
    private readonly Mock<IRefreshTokenService> _refreshTokenService = new();
    private readonly Mock<ITenantProvider> _tenant = new();
    private readonly List<string> _calls = [];

    public TokenServiceAdoptsUserTenantTests()
    {
        _jwtSettings.SetupGet(s => s.Secret).Returns(new string('k', 64));
        _jwtSettings.SetupGet(s => s.Issuer).Returns("cleansia.tests");
        _jwtSettings.SetupGet(s => s.AccessTokenExpMinutes).Returns(15);

        _tenant.Setup(t => t.SetTenantOverride(It.IsAny<string>())).Callback<string>(id => _calls.Add($"override:{id}"));
        _refreshTokenService
            .Setup(s => s.Issue(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns((string userId, bool _, string audience, string? __, string? ___, string? ____) =>
            {
                _calls.Add("issue");
                return new IssuedRefreshToken("raw", RefreshTokenEntity.Create(userId, "hash", DateTimeOffset.UtcNow.AddDays(7), audience, null, null));
            });
    }

    private TokenService Sut() => new(
        _jwtSettings.Object,
        _refreshTokenService.Object,
        Mock.Of<IEmployeeRepository>(),
        Mock.Of<IRequestMetadataProvider>(),
        _tenant.Object,
        TimeProvider.System);

    [Fact]
    public async Task The_Users_Tenant_Becomes_The_Ambient_Tenant_Before_The_Refresh_Token_Is_Issued()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = UserProfile.Customer });
        user.TenantId = "cleansia-sk";

        var response = await Sut().GenerateTokenAsync(user, rememberMe: true, Audience);

        Assert.Equal(["override:cleansia-sk", "issue"], _calls);
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(response.Token).Claims;
        Assert.Equal("cleansia-sk", Assert.Single(claims, c => c.Type == "tenant_id").Value);
    }

    [Fact]
    public async Task An_Unconfirmed_User_Mints_Nothing_But_The_Request_Still_Adopts_Their_Tenant()
    {
        var user = Cleansia.Core.Domain.Users.User.CreateWithPassword("unconfirmed@example.com", "Password1!", "Un", "Confirmed");
        user.TenantId = "cleansia-sk";

        var response = await Sut().GenerateTokenAsync(user, rememberMe: true, Audience);

        Assert.False(response.IsEmailConfirmed);
        Assert.Empty(response.Token);
        Assert.Equal(["override:cleansia-sk"], _calls);
    }
}

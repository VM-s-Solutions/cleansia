using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestConstants = Cleansia.TestUtilities.Constants;
using RefreshTokenEntity = Cleansia.Core.Domain.Users.RefreshToken;
using RefreshTokenCmd = Cleansia.Core.AppServices.Features.Auth.RefreshToken;

namespace Cleansia.IntegrationTests.Features.Auth;

/// <summary>
/// Per-host coverage for ADR-0001 D5 §3: the four non-admin refresh endpoints
/// re-check the user's current DB <see cref="UserProfile"/>, not only the token audience. Each test
/// dispatches a <see cref="RefreshTokenCmd.Command"/> with the exact
/// <c>(RequiredAudience, RequiredProfiles)</c> pair the host's AuthController enriches with, so it
/// exercises the real handler end-to-end against the seeded DB profile:
///   - Web.Customer / Mobile.Customer → (cleansia.customer, Customer)
///   - Web.Partner / Mobile.Partner   → (cleansia.partner, Employee or Administrator — what
///                                       <c>PartnerLogin</c> admits)
/// Mismatch-rejects pin a demoted user whose DB profile no longer matches the host;
/// the match-succeeds path the legitimate case; and the intra-Customer cross-host success (same
/// audience, same profile → no host-binding within the one Customer trust zone) pins the intended
/// non-behavior.
/// </summary>
[Collection("PostgresCollection")]
public class RefreshTokenProfileGateTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private static readonly UserProfile[] CustomerHostProfiles = [UserProfile.Customer];
    private static readonly UserProfile[] PartnerHostProfiles = [UserProfile.Employee, UserProfile.Administrator];

    // Web.Customer: a refresh token for a user whose DB profile is no longer Customer is rejected.
    [Fact]
    public async Task WebCustomer_ProfileNoLongerCustomer_IsRejected()
    {
        await ProfileMismatchIsRejected(
            dbProfile: UserProfile.Employee,
            tokenAudience: JwtAudiences.Customer,
            requiredAudience: JwtAudiences.Customer,
            requiredProfiles: CustomerHostProfiles);
    }

    // Mobile.Customer: same gate as Web.Customer (same audience + profile pair).
    [Fact]
    public async Task MobileCustomer_ProfileNoLongerCustomer_IsRejected()
    {
        await ProfileMismatchIsRejected(
            dbProfile: UserProfile.Administrator,
            tokenAudience: JwtAudiences.Customer,
            requiredAudience: JwtAudiences.Customer,
            requiredProfiles: CustomerHostProfiles);
    }

    // Web.Partner: a refresh token for a user whose DB profile is a Customer is rejected.
    [Fact]
    public async Task WebPartner_ProfileIsCustomer_IsRejected()
    {
        await ProfileMismatchIsRejected(
            dbProfile: UserProfile.Customer,
            tokenAudience: JwtAudiences.Partner,
            requiredAudience: JwtAudiences.Partner,
            requiredProfiles: PartnerHostProfiles);
    }

    // Mobile.Partner: same gate as Web.Partner (its own audience, the same profile set).
    [Fact]
    public async Task MobilePartner_ProfileIsCustomer_IsRejected()
    {
        await ProfileMismatchIsRejected(
            dbProfile: UserProfile.Customer,
            tokenAudience: JwtAudiences.Mobile,
            requiredAudience: JwtAudiences.Mobile,
            requiredProfiles: PartnerHostProfiles);
    }

    // Customer host, DB profile matches → rotation succeeds with a fresh pair.
    [Fact]
    public async Task CustomerHost_ProfileMatches_Succeeds()
    {
        await ProfileMatchSucceeds(
            dbProfile: UserProfile.Customer,
            tokenAudience: JwtAudiences.Customer,
            requiredAudience: JwtAudiences.Customer,
            requiredProfiles: CustomerHostProfiles);
    }

    // Partner host, DB profile matches → rotation succeeds with a fresh pair.
    [Fact]
    public async Task PartnerHost_ProfileMatches_Succeeds()
    {
        await ProfileMatchSucceeds(
            dbProfile: UserProfile.Employee,
            tokenAudience: JwtAudiences.Partner,
            requiredAudience: JwtAudiences.Partner,
            requiredProfiles: PartnerHostProfiles);
    }

    // An administrator signs in on the partner hosts (PartnerLogin admits Administrator), so the
    // session it was handed refreshes there too.
    [Theory]
    [InlineData(JwtAudiences.Partner)]
    [InlineData(JwtAudiences.Mobile)]
    public async Task PartnerHost_Administrator_Succeeds(string audience)
    {
        await ProfileMatchSucceeds(
            dbProfile: UserProfile.Administrator,
            tokenAudience: audience,
            requiredAudience: audience,
            requiredProfiles: PartnerHostProfiles);
    }

    // A Web.Customer refresh token presented to Mobile.Customer: same cleansia.customer
    // audience, same Customer profile → succeeds. The profile gate must NOT add host-binding within
    // the one Customer trust zone (ADR-0001 D5 §2/§3). Pins the intended non-behavior.
    [Fact]
    public async Task WebCustomerTokenAtMobileCustomer_SameAudienceAndProfile_Succeeds()
    {
        await ProfileMatchSucceeds(
            dbProfile: UserProfile.Customer,
            tokenAudience: JwtAudiences.Customer,    // minted by Web.Customer
            requiredAudience: JwtAudiences.Customer, // redeemed at Mobile.Customer — same audience
            requiredProfiles: CustomerHostProfiles);
    }

    private async Task ProfileMismatchIsRejected(
        UserProfile dbProfile, string tokenAudience, string requiredAudience, UserProfile[] requiredProfiles)
    {
        const string rawToken = "live-token-profile-mismatch";

        await TestMethod(
            arrange: context => SeedUserWithLiveToken(context, dbProfile, tokenAudience, rawToken),
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(new RefreshTokenCmd.Command(rawToken)
                {
                    RequiredProfiles = requiredProfiles,
                    RequiredAudience = requiredAudience,
                });
            },
            assert: async (CleansiaDbContext context, BusinessResult<JwtTokenResponse> result) =>
            {
                Assert.False(result.IsSuccess);
                Assert.NotNull(result.Error);
                Assert.Equal(BusinessErrorMessage.InvalidRefreshToken, result.Error!.Message);

                // No new access token, and the presented token was not rotated into a new pair.
                var tokens = await context.RefreshTokens.ToListAsync();
                Assert.Single(tokens);
            });
    }

    private async Task ProfileMatchSucceeds(
        UserProfile dbProfile, string tokenAudience, string requiredAudience, UserProfile[] requiredProfiles)
    {
        const string rawToken = "live-token-profile-match";

        await TestMethod(
            arrange: context => SeedUserWithLiveToken(context, dbProfile, tokenAudience, rawToken),
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(new RefreshTokenCmd.Command(rawToken)
                {
                    RequiredProfiles = requiredProfiles,
                    RequiredAudience = requiredAudience,
                });
            },
            assert: async (CleansiaDbContext context, BusinessResult<JwtTokenResponse> result) =>
            {
                Assert.True(result.IsSuccess);
                Assert.False(string.IsNullOrEmpty(result.Value.Token));
                Assert.False(string.IsNullOrEmpty(result.Value.RefreshToken));
                Assert.Equal(dbProfile.ToString(), result.Value.Role);

                var tokens = await context.RefreshTokens.OrderBy(t => t.CreatedOn).ToListAsync();
                Assert.Equal(2, tokens.Count);
                Assert.Equal("rotated", tokens[0].RevokedReason);
                Assert.All(tokens, t => Assert.Equal(tokenAudience, t.Audience));
            });
    }

    private static async Task SeedUserWithLiveToken(
        CleansiaDbContext context, UserProfile dbProfile, string audience, string rawToken)
    {
        context.Languages.Add(Language.Create("en", "English"));
        await context.SaveChangesAsync();

        var user = User.CreateWithPassword(
            email: TestConstants.TestUserSession.TestUserEmail,
            password: TestConstants.TestUserSession.TestUserPassword,
            firstName: TestConstants.TestUserSession.TestFirstName,
            lastName: TestConstants.TestUserSession.TestLastName,
            profile: dbProfile,
            adminRole: dbProfile == UserProfile.Administrator ? AdminRole.Administrator : null);
        user.ConfirmEmail();
        context.Users.Add(user);
        await context.CommitAsync(CancellationToken.None);

        var token = RefreshTokenEntity.Create(
            userId: user.Id,
            tokenHash: HashToken(rawToken),
            expiresAt: DateTimeOffset.UtcNow.AddDays(7),
            audience: audience,
            deviceLabel: null,
            ipAddress: null);
        token.Created("test", DateTimeOffset.UtcNow);
        context.RefreshTokens.Add(token);
        await context.CommitAsync(CancellationToken.None);
    }

    private static string HashToken(string raw)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

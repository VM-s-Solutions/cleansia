using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestConstants = Cleansia.TestUtilities.Constants;
using RefreshTokenEntity = Cleansia.Core.Domain.Users.RefreshToken;
using RefreshTokenCmd = Cleansia.Core.AppServices.Features.Auth.RefreshToken;
using LogoutCmd = Cleansia.Core.AppServices.Features.Auth.Logout;

namespace Cleansia.IntegrationTests.Features.Auth;

/// <summary>
/// End-to-end coverage of the refresh-token lifecycle. Exercises the real
/// <see cref="Cleansia.Core.AppServices.Services.RefreshTokenService"/> through
/// its MediatR commands; no mocks. Each test starts from a seeded, confirmed
/// user and issues a refresh token via the real login pipeline.
/// </summary>
[Collection("PostgresCollection")]
public class RefreshTokenFlowTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    [Fact]
    public async Task HappyPath_RefreshTokenRotatesAndIssuesNewPair()
    {
        string? originalRefreshToken = null;

        await TestMethod(
            arrange: SeedConfirmedUser,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var loginResult = await Login(mediator);
                originalRefreshToken = loginResult.Value.RefreshToken;
                return await Refresh(mediator, originalRefreshToken!);
            },
            assert: async (CleansiaDbContext context, BusinessResult<JwtTokenResponse> result) =>
            {
                Assert.True(result.IsSuccess);
                Assert.False(string.IsNullOrEmpty(result.Value.Token));
                Assert.False(string.IsNullOrEmpty(result.Value.RefreshToken));
                Assert.NotEqual(originalRefreshToken, result.Value.RefreshToken);

                var tokens = await context.RefreshTokens.OrderBy(t => t.CreatedOn).ToListAsync();
                Assert.Equal(2, tokens.Count);
                Assert.Equal("rotated", tokens[0].RevokedReason);
                Assert.NotNull(tokens[0].RevokedAt);
                Assert.Equal(tokens[1].Id, tokens[0].ReplacedByTokenId);
                Assert.Null(tokens[1].RevokedAt);
            });
    }

    [Fact]
    public async Task ReusedRotatedToken_IsTreatedAsTheftAndRevokesChain()
    {
        await TestMethod(
            arrange: SeedConfirmedUser,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var loginResult = await Login(mediator);
                var stolen = loginResult.Value.RefreshToken!;

                await Refresh(mediator, stolen);       // legit rotation
                return await Refresh(mediator, stolen); // theft: reused
            },
            assert: async (CleansiaDbContext context, BusinessResult<JwtTokenResponse> result) =>
            {
                Assert.False(result.IsSuccess);
                Assert.NotNull(result.Error);
                Assert.Equal(BusinessErrorMessage.RefreshTokenReused, result.Error!.Message);

                var tokens = await context.RefreshTokens.ToListAsync();
                Assert.All(tokens, t => Assert.NotNull(t.RevokedAt));
                Assert.Contains(tokens, t => t.RevokedReason == "security");
            });
    }

    [Fact]
    public async Task UnknownToken_IsRejected()
    {
        await TestMethod(
            arrange: SeedConfirmedUser,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await Refresh(mediator, "definitely-not-a-real-token-abc123");
            },
            assert: (CleansiaDbContext _, BusinessResult<JwtTokenResponse> result) =>
            {
                Assert.False(result.IsSuccess);
                Assert.NotNull(result.Error);
                Assert.Equal(BusinessErrorMessage.InvalidRefreshToken, result.Error!.Message);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        const string rawToken = "expired-raw-token-for-test";

        await TestMethod(
            arrange: async (CleansiaDbContext context) =>
            {
                await SeedConfirmedUser(context);

                // Directly create an already-expired row — the service itself refuses
                // to issue expired tokens, so we side-step it here.
                var user = await context.Users.FirstAsync();
                var hash = HashToken(rawToken);
                var expiredToken = RefreshTokenEntity.Create(
                    userId: user.Id,
                    tokenHash: hash,
                    expiresAt: DateTimeOffset.UtcNow.AddMinutes(-5),
                    deviceLabel: null,
                    ipAddress: null);
                expiredToken.Created("test", DateTimeOffset.UtcNow.AddDays(-31));
                context.RefreshTokens.Add(expiredToken);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await Refresh(mediator, rawToken);
            },
            assert: (CleansiaDbContext _, BusinessResult<JwtTokenResponse> result) =>
            {
                Assert.False(result.IsSuccess);
                Assert.NotNull(result.Error);
                Assert.Equal(BusinessErrorMessage.InvalidRefreshToken, result.Error!.Message);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken_AndBlocksSubsequentRefresh()
    {
        await TestMethod(
            arrange: SeedConfirmedUser,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var loginResult = await Login(mediator);
                var refreshToken = loginResult.Value.RefreshToken!;

                await Logout(mediator, refreshToken);

                return await Refresh(mediator, refreshToken);
            },
            assert: async (CleansiaDbContext context, BusinessResult<JwtTokenResponse> result) =>
            {
                Assert.False(result.IsSuccess);
                Assert.NotNull(result.Error);
                Assert.Equal(BusinessErrorMessage.InvalidRefreshToken, result.Error!.Message);

                var tokens = await context.RefreshTokens.ToListAsync();
                Assert.Single(tokens);
                Assert.Equal("logout", tokens[0].RevokedReason);
            });
    }

    [Fact]
    public async Task Logout_UnknownToken_IsIdempotent()
    {
        await TestMethod(
            arrange: SeedConfirmedUser,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await Logout(mediator, "nonexistent-token-xyz");
            },
            assert: (CleansiaDbContext _, BusinessResult<bool> result) =>
            {
                // Idempotent: never leaks whether the token existed. Always succeeds.
                Assert.True(result.IsSuccess);
                return Task.CompletedTask;
            });
    }

    // ─── Typed MediatR helpers ───
    // MediatR's Send<T> is weakly typed; wrap it so we get a concrete BusinessResult
    // everywhere without inline casts.

    private static async Task<BusinessResult<JwtTokenResponse>> Login(IMediator mediator) =>
        await mediator.Send(new Login.Command(
            Email: TestConstants.TestUserSession.TestUserEmail,
            Password: TestConstants.TestUserSession.TestUserPassword,
            RememberMe: true));

    private static async Task<BusinessResult<JwtTokenResponse>> Refresh(IMediator mediator, string token) =>
        await mediator.Send(new RefreshTokenCmd.Command(token));

    private static async Task<BusinessResult<bool>> Logout(IMediator mediator, string token) =>
        await mediator.Send(new LogoutCmd.Command(token));

    private static async Task SeedConfirmedUser(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        await context.SaveChangesAsync();

        var user = User.CreateWithPassword(
            email: TestConstants.TestUserSession.TestUserEmail,
            password: TestConstants.TestUserSession.TestUserPassword,
            firstName: TestConstants.TestUserSession.TestFirstName,
            lastName: TestConstants.TestUserSession.TestLastName);
        user.ConfirmEmail();
        context.Users.Add(user);
        await context.CommitAsync(CancellationToken.None);
    }

    private static string HashToken(string raw)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

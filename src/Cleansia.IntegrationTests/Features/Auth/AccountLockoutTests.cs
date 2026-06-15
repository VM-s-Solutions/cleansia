using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Constants = Cleansia.TestUtilities.Constants;
using RefreshTokenEntity = Cleansia.Core.Domain.Users.RefreshToken;

namespace Cleansia.IntegrationTests.Features.Auth;

/// <summary>
/// Per-account login lockout, end-to-end through the real pipeline and Postgres.
/// The guard keys on the persisted account row — the Login command carries no caller IP at all — so
/// the lockout holds for an attempt arriving from any source, which is exactly what bounds a
/// distributed (multi-IP) credential-stuffing run. Each attempt runs in its own DI scope (its own
/// DbContext) to mirror production per-request scoping and prove the state is read from the store.
/// </summary>
[Collection("PostgresCollection")]
public class AccountLockoutTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string WrongPassword = Constants.TestUserSession.TestUserPassword + "wrong";

    private static async Task SeedConfirmedUser(Infra.Database.CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        await context.SaveChangesAsync();

        var user = User.CreateWithPassword(
            email: Constants.TestUserSession.TestUserEmail,
            password: Constants.TestUserSession.TestUserPassword,
            firstName: Constants.TestUserSession.TestFirstName,
            lastName: Constants.TestUserSession.TestLastName);
        user.ConfirmEmail();
        user.Created(Constants.TestUserSession.TestUserId, DateTime.UtcNow);
        context.Users.Add(user);
    }

    private static async Task<BusinessResult<JwtTokenResponse>> LoginInFreshScope(IServiceProvider provider, string password, string? trustedDeviceToken = null)
    {
        // A fresh scope per attempt = a fresh DbContext, as in production where every request is its
        // own scope; nothing can leak through a tracked in-memory entity.
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(new Login.Command(Constants.TestUserSession.TestUserEmail, password, true)
        {
            TrustedDeviceToken = trustedDeviceToken,
        });
    }

    private static string HashToken(string raw)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static async Task<string> SeedTrustedRefreshTokenForUser(Infra.Database.CleansiaDbContext context)
    {
        const string raw = "trusted-device-refresh-token-integration";
        var user = await context.Users.SingleAsync();
        var token = RefreshTokenEntity.Create(
            userId: user.Id,
            tokenHash: HashToken(raw),
            expiresAt: DateTimeOffset.UtcNow.AddDays(7),
            audience: "audience",
            deviceLabel: null,
            ipAddress: null);
        token.Created("test", DateTimeOffset.UtcNow);
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync();
        return raw;
    }

    [Fact]
    public async Task After_Five_Wrong_Passwords_Even_The_Correct_Password_Is_Refused()
    {
        await TestMethod(
            arrange: SeedConfirmedUser,
            act: async provider =>
            {
                for (var attempt = 0; attempt < User.MaxFailedLoginAttempts; attempt++)
                {
                    var failed = await LoginInFreshScope(provider, WrongPassword);
                    Assert.False(failed.IsSuccess);
                }

                return await LoginInFreshScope(provider, Constants.TestUserSession.TestUserPassword);
            },
            assert: async (context, result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Contains(validation.Errors, e => e.Message == BusinessErrorMessage.AccountLocked);

                var user = await context.Users.SingleAsync();
                Assert.NotNull(user.LockoutEndsAt);
                Assert.True(user.LockoutEndsAt > DateTimeOffset.UtcNow);
                Assert.Equal(0, user.FailedLoginAttempts);
            });
    }

    [Fact]
    public async Task Failed_Attempts_Are_Persisted_To_The_Store()
    {
        await TestMethod(
            arrange: SeedConfirmedUser,
            act: async provider =>
            {
                await LoginInFreshScope(provider, WrongPassword);
                return await LoginInFreshScope(provider, WrongPassword);
            },
            assert: async (context, result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Contains(validation.Errors, e => e.Message == BusinessErrorMessage.InvalidPassword);

                var user = await context.Users.SingleAsync();
                Assert.Equal(2, user.FailedLoginAttempts);
                Assert.Null(user.LockoutEndsAt);
            });
    }

    [Fact]
    public async Task A_Couple_Of_Failures_Then_Success_Issues_A_Token_And_Clears_The_Counter()
    {
        await TestMethod(
            arrange: SeedConfirmedUser,
            act: async provider =>
            {
                await LoginInFreshScope(provider, WrongPassword);
                await LoginInFreshScope(provider, WrongPassword);
                return await LoginInFreshScope(provider, Constants.TestUserSession.TestUserPassword);
            },
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess);
                Assert.NotEmpty(result.Value.Token);

                var user = await context.Users.SingleAsync();
                Assert.Equal(0, user.FailedLoginAttempts);
                Assert.Null(user.LockoutEndsAt);
            });
    }

    [Fact]
    public async Task A_Trusted_Device_With_A_Valid_Account_Token_Logs_In_Despite_The_Lockout()
    {
        string trustedToken = null!;

        await TestMethod(
            arrange: async context =>
            {
                await SeedConfirmedUser(context);
                await context.SaveChangesAsync();
                trustedToken = await SeedTrustedRefreshTokenForUser(context);
            },
            act: async provider =>
            {
                for (var attempt = 0; attempt < User.MaxFailedLoginAttempts; attempt++)
                {
                    await LoginInFreshScope(provider, WrongPassword);
                }

                return await LoginInFreshScope(provider, Constants.TestUserSession.TestUserPassword, trustedToken);
            },
            assert: (_, result) =>
            {
                Assert.True(result.IsSuccess);
                Assert.NotEmpty(result.Value.Token);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task A_Locked_Account_With_A_Token_Bound_To_Another_User_Stays_Locked()
    {
        const string foreignRaw = "refresh-token-for-some-other-account";

        await TestMethod(
            arrange: async context =>
            {
                await SeedConfirmedUser(context);
                await context.SaveChangesAsync();

                // The foreign token references another user — RefreshTokens.UserId FKs to Users, so the
                // owning row must exist (distinct email; audit-stamped to satisfy CreatedBy NOT NULL).
                var otherUser = User.CreateWithPassword(
                    email: "another-user@example.com",
                    password: Constants.TestUserSession.TestUserPassword,
                    firstName: "Other",
                    lastName: "Account");
                otherUser.Id = "another-users-id";
                otherUser.ConfirmEmail();
                otherUser.Created("test", DateTime.UtcNow);
                context.Users.Add(otherUser);
                await context.SaveChangesAsync();

                var foreign = RefreshTokenEntity.Create(
                    userId: "another-users-id",
                    tokenHash: HashToken(foreignRaw),
                    expiresAt: DateTimeOffset.UtcNow.AddDays(7),
                    audience: "audience",
                    deviceLabel: null,
                    ipAddress: null);
                foreign.Created("test", DateTimeOffset.UtcNow);
                context.RefreshTokens.Add(foreign);
                await context.SaveChangesAsync();
            },
            act: async provider =>
            {
                for (var attempt = 0; attempt < User.MaxFailedLoginAttempts; attempt++)
                {
                    await LoginInFreshScope(provider, WrongPassword);
                }

                return await LoginInFreshScope(provider, Constants.TestUserSession.TestUserPassword, foreignRaw);
            },
            assert: (_, result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Contains(validation.Errors, e => e.Message == BusinessErrorMessage.AccountLocked);
                return Task.CompletedTask;
            });
    }
}

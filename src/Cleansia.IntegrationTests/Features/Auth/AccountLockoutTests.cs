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

    private static async Task<BusinessResult<JwtTokenResponse>> LoginInFreshScope(IServiceProvider provider, string password)
    {
        // A fresh scope per attempt = a fresh DbContext, as in production where every request is its
        // own scope; nothing can leak through a tracked in-memory entity.
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(new Login.Command(Constants.TestUserSession.TestUserEmail, password, true));
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
}

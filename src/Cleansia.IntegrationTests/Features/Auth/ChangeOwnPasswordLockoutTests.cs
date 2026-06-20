using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Constants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Auth;

/// <summary>
/// Per-account current-password attempt budget on the authenticated change-password surface,
/// end-to-end against Postgres. The budget is the SAME lockout pair the login surfaces use
/// (FailedLoginAttempts/LockoutEndsAt), charged by an atomic conditional UPDATE, so a wrong
/// current-password spray opens the lockout window: once the budget is spent the (N+1)-th attempt is
/// refused with the distinct AccountLocked key and the current password is no longer evaluated. A
/// successful change restores a fresh budget.
/// </summary>
[Collection("PostgresCollection")]
public class ChangeOwnPasswordLockoutTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrentPassword = Constants.TestUserSession.TestUserPassword;
    private const string NewPassword = "BrandNew123";

    private static User CallerWithCurrentPassword()
    {
        var user = User.CreateWithPassword(
            Constants.TestUserSession.TestUserEmail,
            CurrentPassword,
            Constants.TestUserSession.TestFirstName,
            Constants.TestUserSession.TestLastName,
            UserProfile.Administrator);
        user.ConfirmEmail();
        user.Created(Constants.TestUserSession.TestUserId, DateTime.UtcNow);
        user.Id = Constants.TestUserSession.TestUserId;
        return user;
    }

    private static async Task SeedLanguageAnd(Infra.Database.CleansiaDbContext context, User user)
    {
        context.Languages.Add(Language.Create("en", "English"));
        await context.SaveChangesAsync();
        context.Users.Add(user);
    }

    [Fact]
    public async Task After_The_Budget_Is_Spent_Even_The_Correct_Current_Password_Is_Refused()
    {
        var caller = CallerWithCurrentPassword();

        await TestMethod(
            arrange: context => SeedLanguageAnd(context, caller),
            act: async provider =>
            {
                for (var attempt = 0; attempt < User.MaxFailedLoginAttempts; attempt++)
                {
                    using var scope = provider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    var sprayed = await mediator.Send(new ChangeOwnPassword.Command($"wrong-{attempt}", NewPassword));
                    Assert.False(sprayed.IsSuccess);
                }

                using var finalScope = provider.CreateScope();
                var finalMediator = finalScope.ServiceProvider.GetRequiredService<IMediator>();
                return await finalMediator.Send(new ChangeOwnPassword.Command(CurrentPassword, NewPassword));
            },
            assert: async (context, result) =>
            {
                Assert.False(result.IsSuccess);
                Assert.Equal(BusinessErrorMessage.AccountLocked, result.Error!.Message);

                var stored = await context.Users.SingleAsync();
                Assert.True(stored.IsLockedOut(DateTimeOffset.UtcNow));
                // The correct password was never rotated — the lockout gate refused it (no oracle).
                Assert.True(CurrentPassword.CheckIfPasswordSame(stored.Password!));
            });
    }

    [Fact]
    public async Task A_Wrong_Attempt_Below_The_Cap_Charges_The_Budget_But_Does_Not_Lock()
    {
        var caller = CallerWithCurrentPassword();

        await TestMethod(
            arrange: context => SeedLanguageAnd(context, caller),
            act: async provider =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                return await mediator.Send(new ChangeOwnPassword.Command("totally-wrong", NewPassword));
            },
            assert: async (context, result) =>
            {
                Assert.False(result.IsSuccess);
                Assert.Equal(BusinessErrorMessage.CurrentPasswordInvalid, result.Error!.Message);

                var stored = await context.Users.SingleAsync();
                Assert.Equal(1, stored.FailedLoginAttempts);
                Assert.Null(stored.LockoutEndsAt);
            });
    }

    [Fact]
    public async Task A_Successful_Change_Restores_A_Fresh_Budget()
    {
        var caller = CallerWithCurrentPassword();

        await TestMethod(
            arrange: context => SeedLanguageAnd(context, caller),
            act: async provider =>
            {
                using var wrongScope = provider.CreateScope();
                var wrongMediator = wrongScope.ServiceProvider.GetRequiredService<IMediator>();
                await wrongMediator.Send(new ChangeOwnPassword.Command("wrong-once", NewPassword));

                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                return await mediator.Send(new ChangeOwnPassword.Command(CurrentPassword, NewPassword));
            },
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess);

                var stored = await context.Users.SingleAsync();
                Assert.Equal(0, stored.FailedLoginAttempts);
                Assert.Null(stored.LockoutEndsAt);
                Assert.True(NewPassword.CheckIfPasswordSame(stored.Password!));
            });
    }
}

using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Constants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Users;

/// <summary>
/// Per-reset-code attempt cap, end-to-end against Postgres. The reset command is
/// email-bound, so a wrong-code spray against one account burns that account's persisted per-code
/// budget — after N wrong guesses the (N+1)-th attempt is refused even though it carries the CORRECT
/// code, which is exactly the "a spray that would eventually hit a valid code is stopped at the cap"
/// guarantee. A freshly issued code re-grants the budget, so the honest owner recovers by re-requesting.
/// </summary>
[Collection("PostgresCollection")]
public class ChangePasswordResetCodeCapTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string NewPassword = "BrandNew123";

    [Fact]
    public async Task A_Wrong_Code_Spray_Is_Stopped_At_The_Cap_Before_The_Valid_Code_Succeeds()
    {
        string rawResetCode = null!;

        await TestMethod(
            arrange: async context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                await context.SaveChangesAsync();

                var user = User.CreateWithPassword(
                    Constants.TestUserSession.TestUserEmail,
                    Constants.TestUserSession.TestUserPassword,
                    Constants.TestUserSession.TestFirstName,
                    Constants.TestUserSession.TestLastName);
                user.ConfirmEmail();
                rawResetCode = user.UpdateResetPasswordToken();
                context.Users.Add(user);
            },
            act: async provider =>
            {
                for (var attempt = 0; attempt < User.MaxCodeVerificationAttempts; attempt++)
                {
                    using var scope = provider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    var sprayed = await mediator.Send(new ChangePassword.Command(
                        Constants.TestUserSession.TestUserEmail, NewPassword, $"wrong-guess-{attempt}"));
                    Assert.False(sprayed.IsSuccess);
                }

                using var finalScope = provider.CreateScope();
                var finalMediator = finalScope.ServiceProvider.GetRequiredService<IMediator>();
                return await finalMediator.Send(new ChangePassword.Command(
                    Constants.TestUserSession.TestUserEmail, NewPassword, rawResetCode));
            },
            assert: async (context, result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Contains(validation.Errors, e => e.Message == BusinessErrorMessage.TooManyAttempts);

                var stored = await context.Users.SingleAsync();
                Assert.Equal(User.MaxCodeVerificationAttempts, stored.ResetPasswordCodeAttempts);
                Assert.NotNull(stored.ResetPasswordCode);
            });
    }

    [Fact]
    public async Task The_Correct_Code_On_The_First_Try_Resets_The_Password_And_Clears_The_Budget()
    {
        string rawResetCode = null!;

        await TestMethod(
            arrange: async context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                await context.SaveChangesAsync();

                var user = User.CreateWithPassword(
                    Constants.TestUserSession.TestUserEmail,
                    Constants.TestUserSession.TestUserPassword,
                    Constants.TestUserSession.TestFirstName,
                    Constants.TestUserSession.TestLastName);
                user.ConfirmEmail();
                rawResetCode = user.UpdateResetPasswordToken();
                context.Users.Add(user);
            },
            act: async provider =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                return await mediator.Send(new ChangePassword.Command(
                    Constants.TestUserSession.TestUserEmail, NewPassword, rawResetCode));
            },
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess);

                var stored = await context.Users.SingleAsync();
                // One-shot: the consumed code is cleared, so no budget remains to attack.
                Assert.Null(stored.ResetPasswordCode);
                Assert.Null(stored.ResetPasswordCodeExpiresAt);
            });
    }
}

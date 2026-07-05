using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Constants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Auth;

/// <summary>
/// Per-confirmation-code attempt cap, end-to-end against Postgres (AC1/AC4 of the
/// rate-limit fast-follow). The budget lives on the persisted account row and is consumed by an
/// atomic conditional UPDATE, so once N verification attempts have been charged the (N+1)-th attempt
/// is refused BEFORE a confirmation can succeed — even when it carries the correct live code.
/// </summary>
[Collection("PostgresCollection")]
public class ConfirmationCodeAttemptCapTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private static User NewUnconfirmedUser()
    {
        var user = User.CreateWithPassword(
            Constants.TestUserSession.TestUserEmail,
            Constants.TestUserSession.TestUserPassword,
            Constants.TestUserSession.TestFirstName,
            Constants.TestUserSession.TestLastName);
        user.Created(Constants.TestUserSession.TestUserId, DateTime.UtcNow);
        return user;
    }

    private static async Task SeedLanguageAnd(Infra.Database.CleansiaDbContext context, User user)
    {
        context.Languages.Add(Language.Create("en", "English"));
        await context.SaveChangesAsync();
        context.Users.Add(user);
    }

    [Fact]
    public async Task Once_The_Budget_Is_Spent_Even_The_Correct_Live_Code_Cannot_Confirm()
    {
        var user = NewUnconfirmedUser();

        await TestMethod(
            arrange: context => SeedLanguageAnd(context, user),
            act: async provider =>
            {
                var repository = provider.GetRequiredService<IUserRepository>();
                for (var attempt = 0; attempt < User.MaxCodeVerificationAttempts; attempt++)
                {
                    Assert.True(await repository.TryChargeConfirmationCodeAttemptAsync(user.Id, CancellationToken.None));
                }

                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                return await mediator.Send(new ConfirmUserEmail.Command(user.RawConfirmationToken!, user.Email));
            },
            assert: async (context, result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Contains(validation.Errors, e => e.Message == BusinessErrorMessage.TooManyAttempts);

                var stored = await context.Users.SingleAsync();
                Assert.False(stored.IsEmailConfirmed);
                Assert.NotNull(stored.ConfirmationCode);
                Assert.Equal(User.MaxCodeVerificationAttempts, stored.ConfirmationCodeAttempts);
            });
    }

    [Fact]
    public async Task Expired_Code_Retries_Are_Charged_To_The_Account_In_The_Store()
    {
        var user = NewUnconfirmedUser();

        await TestMethod(
            arrange: async context =>
            {
                await SeedLanguageAnd(context, user);
                user.Merge(new UserMockFactory.UserPartial
                {
                    ConfirmationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                });
            },
            act: async provider =>
            {
                BusinessResult<JwtTokenResponse> last = null!;
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    using var scope = provider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    last = await mediator.Send(new ConfirmUserEmail.Command(user.RawConfirmationToken!, user.Email));
                }

                return last;
            },
            assert: async (context, result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Contains(validation.Errors, e => e.Message == BusinessErrorMessage.InvalidConfirmationCode);

                var stored = await context.Users.SingleAsync();
                Assert.False(stored.IsEmailConfirmed);
                Assert.Equal(2, stored.ConfirmationCodeAttempts);
            });
    }

    [Fact]
    public async Task A_First_Try_Valid_Code_Confirms_Without_Tripping_The_Cap()
    {
        var user = NewUnconfirmedUser();

        await TestMethod(
            arrange: context => SeedLanguageAnd(context, user),
            act: async provider =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                return await mediator.Send(new ConfirmUserEmail.Command(user.RawConfirmationToken!, user.Email));
            },
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess);
                Assert.NotEmpty(result.Value.Token);

                var stored = await context.Users.SingleAsync();
                Assert.True(stored.IsEmailConfirmed);
                // The code is consumed one-shot; the next issued code re-grants the attempt budget.
                Assert.Null(stored.ConfirmationCode);
            });
    }
}

using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Auth;

[Collection("PostgresCollection")]
public class ConfirmUserEmailTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    [Fact]
    public async Task ShouldConfirmEmailAndReturnToken()
    {
        var user = User.CreateWithPassword(
            Constants.TestUserSession.TestUserEmail,
            Constants.TestUserSession.TestUserPassword,
            Constants.TestUserSession.TestFirstName,
            Constants.TestUserSession.TestLastName);
        user.Created(Constants.TestUserSession.TestUserId, DateTime.UtcNow);

        await TestMethod(
            arrange: async context =>
            {
                // Seed required language before creating user (FK constraint)
                context.Languages.Add(Language.Create("en", "English"));
                await context.SaveChangesAsync();

                context.Users.Add(user);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                // Submit the RAW 6-digit code + the account email: an OTP is guessable in
                // isolation, so it is never resolved by the bare code — the email names the
                // account whose stored hash the code is compared against.
                var command = new ConfirmUserEmail.Command(user.RawConfirmationToken!, user.Email);
                return await mediator.Send(command);
            },
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess);
                var tokenResponse = result.Value;
                Assert.NotEmpty(tokenResponse.Token);
                Assert.True(tokenResponse.IsEmailConfirmed);
                var confirmedUser = await context.Users.FirstAsync();
                Assert.True(confirmedUser.IsEmailConfirmed);
                Assert.Null(confirmedUser.ConfirmationCode);
            }
        );
    }
}
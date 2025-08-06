using Cleansia.Core.AppServices.Features.Auth;
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
                context.Users.Add(user);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var command = new ConfirmUserEmail.Command(user.ConfirmationCode);
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
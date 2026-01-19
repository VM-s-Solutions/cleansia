using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Auth;

[Collection("PostgresCollection")]
public class LoginTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    [Fact]
    public async Task ShouldLoginUserAndReturnTokenWithNoExistingOrders()
    {
        await TestMethod(
        arrange: async context =>
        {
            // Seed required language before creating user (FK constraint)
            context.Languages.Add(Language.Create("en", "English"));
            await context.SaveChangesAsync();

            var user = User.CreateWithPassword(
                email: Constants.TestUserSession.TestUserEmail,
                password: Constants.TestUserSession.TestUserPassword,
                firstName: Constants.TestUserSession.TestFirstName,
                lastName: Constants.TestUserSession.TestLastName);
            user.ConfirmEmail();
            context.Users.Add(user);
            await context.CommitAsync(CancellationToken.None);
        },
        act: async provider =>
        {
            var mediator = provider.GetRequiredService<IMediator>();
            var command = new Login.Command(
                Email: Constants.TestUserSession.TestUserEmail,
                Password: Constants.TestUserSession.TestUserPassword,
                RememberMe: true);
            return await mediator.Send(command);
        },
        assert: async (context, result) =>
        {
            Assert.True(result.IsSuccess);
            var tokenResponse = result.Value;
            Assert.NotEmpty(tokenResponse.Token);
            Assert.True(tokenResponse.IsEmailConfirmed);

            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == Constants.TestUserSession.TestUserEmail);
            Assert.NotNull(user);
            Assert.Equal(AuthenticationType.Internal, user.AuthenticationType);
            Assert.Equal(Constants.TestUserSession.TestFirstName, user.FirstName);
            Assert.Equal(Constants.TestUserSession.TestLastName, user.LastName);
            Assert.True(user.IsEmailConfirmed);

            var orders = await context.Orders.Where(o => o.UserId == user.Id).ToListAsync();
            Assert.Empty(orders);
        });
    }
}
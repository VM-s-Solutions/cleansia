using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Auth;

[Collection("PostgresCollection")]
public class GoogleAuthTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    [Fact]
    public async Task ShouldCreateNewUserAndReturnTokenForValidGoogleAuth()
    {
        await TestMethod(
            arrange: async context =>
            {
                // Seed required language before creating user (FK constraint)
                context.Languages.Add(Language.Create("en", "English"));
                await context.SaveChangesAsync();
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var command = new GoogleAuth.Command(
                    Token: "valid-test-token",
                    GoogleId: "google123",
                    Email: Constants.TestUserSession.TestUserEmail,
                    FirstName: Constants.TestUserSession.TestFirstName,
                    LastName: Constants.TestUserSession.TestLastName);
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
                Assert.Equal("google123", user.GoogleId);
                Assert.Equal(AuthenticationType.Google, user.AuthenticationType);
                Assert.Equal(Constants.TestUserSession.TestFirstName, user.FirstName);
                Assert.Equal(Constants.TestUserSession.TestLastName, user.LastName);
                Assert.True(user.IsEmailConfirmed);

                var cart = await context.Carts.FirstOrDefaultAsync(c => c.UserId == user.Id);
                Assert.NotNull(cart);

                var orders = await context.Orders.Where(o => o.UserId == user.Id).ToListAsync();
                Assert.Empty(orders);
            });
    }
}
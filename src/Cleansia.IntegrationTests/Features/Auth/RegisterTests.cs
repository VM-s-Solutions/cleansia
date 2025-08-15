using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.Auth;

[Collection("PostgresCollection")]
public class RegisterTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    [Fact]
    public async Task ShouldRegisterNewUserWithNoExistingOrders()
    {
        await TestMethod(
            setup: services =>
            {
                services.Replace(ServiceDescriptor.Scoped<IEmailService>(_ => new Mock<IEmailService>().Object));
            },
            arrange: _ => { },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var command = new Register.Command(
                    Email: Constants.TestUserSession.TestUserEmail,
                    Password: Constants.TestUserSession.TestUserPassword,
                    FirstName: Constants.TestUserSession.TestFirstName,
                    LastName: Constants.TestUserSession.TestLastName
                );
                return await mediator.Send(command);
            },
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess);
                Assert.True(result.Value);

                var user = await context.Users.FirstOrDefaultAsync(u => u.Email == Constants.TestUserSession.TestUserEmail);
                Assert.NotNull(user);
                Assert.Equal(AuthenticationType.Internal, user.AuthenticationType);
                Assert.Equal(Constants.TestUserSession.TestFirstName, user.FirstName);
                Assert.Equal(Constants.TestUserSession.TestLastName, user.LastName);
                Assert.False(user.IsEmailConfirmed);
                Assert.NotNull(user.ConfirmationCode);

                var cart = await context.Carts.FirstOrDefaultAsync(c => c.UserId == user.Id);
                Assert.NotNull(cart);

                var orders = await context.Orders.Where(o => o.UserId == user.Id).ToListAsync();
                Assert.Empty(orders);
            }
        );
    }
}
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
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
                return Task.CompletedTask;
            },
            arrange: async context =>
            {
                // Seed required language before creating user (FK constraint)
                context.Languages.Add(Language.Create("cz", "Czech"));
                await context.SaveChangesAsync();
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var command = new Register.Command(
                    Email: Constants.TestUserSession.TestUserEmail,
                    Password: Constants.TestUserSession.TestUserPassword,
                    FirstName: Constants.TestUserSession.TestFirstName,
                    LastName: Constants.TestUserSession.TestLastName,
                    "cz"
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
                // T-0106 / IDA-SEC-03: a token was issued, but the PERSISTED value is the SHA-256 HASH
                // (64 hex chars) — never the 6-digit plaintext code. The raw token left only in the email.
                Assert.NotNull(user.ConfirmationCode);
                Assert.Equal(64, user.ConfirmationCode!.Length);
                Assert.Matches("^[0-9a-f]{64}$", user.ConfirmationCode);

                var cart = await context.Carts.FirstOrDefaultAsync(c => c.UserId == user.Id);
                Assert.NotNull(cart);

                var orders = await context.Orders.Where(o => o.UserId == user.Id).ToListAsync();
                Assert.Empty(orders);
            },
            transactional: false
        );
    }
}
using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities;
using Moq;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// Owner ruling 2026-10-26, "I select option B": a signed-in customer's My orders also lists the bookings made
/// without an account under their e-mail. Only a Customer session lists them, by the session's e-mail: staff
/// accounts are signed in on the customer hosts too, and an administrator's address was never proven by its
/// mailbox.
/// </summary>
public sealed class OrderAccessServiceListedGuestTests
{
    private const string UserId = "user-listed-guest";
    private const string Email = "jana.novak@cleansia.test";

    [Fact]
    public void A_Signed_In_Customer_Lists_Guest_Bookings_Under_The_Sessions_Email()
    {
        Assert.Equal(Email, ListedGuestEmailFor(new TestUserSessionProvider(UserId, Email, Role(UserProfile.Customer))));
    }

    [Theory]
    [InlineData(UserProfile.Employee)]
    [InlineData(UserProfile.Administrator)]
    public void A_Staff_Session_Lists_None(UserProfile profile)
    {
        Assert.Null(ListedGuestEmailFor(new TestUserSessionProvider(UserId, Email, Role(profile))));
    }

    [Fact]
    public void A_Session_Naming_No_Role_Lists_None()
    {
        Assert.Null(ListedGuestEmailFor(new TestUserSessionProvider(UserId, Email)));
    }

    [Fact]
    public void A_Customer_Session_Naming_No_User_Lists_None()
    {
        Assert.Null(ListedGuestEmailFor(new TestUserSessionProvider(
            [new Claim(ClaimTypes.Email, Email), new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())])));
    }

    private static Claim[] Role(UserProfile profile) => [new Claim(ClaimTypes.Role, profile.ToString())];

    private static string? ListedGuestEmailFor(IUserSessionProvider session) =>
        new OrderAccessService(
                session,
                Mock.Of<IEmployeeRepository>(),
                Mock.Of<IOrderRepository>(),
                Mock.Of<ICurrencyResolutionService>(),
                Mock.Of<ICashLedgerRepository>(),
                Mock.Of<IAppConfigurationProvider>())
            .ListedGuestEmailForCaller();
}

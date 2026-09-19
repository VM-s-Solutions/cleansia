using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Features.Orders.Filters;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// The admin order list scoped to ONE account: <c>Filter.UserId</c> returns the orders booked on that
/// account and nothing else — not another account's, and not a guest booking that merely names the
/// account's e-mail (a guest booking has no UserId and is never attached to an account afterwards). The
/// term is admin-only, like the customer name/e-mail/phone terms beside it: a cleaner passing a UserId
/// is not narrowed, so the cleaner surface cannot be used to learn whether an account has bookings.
/// </summary>
[Collection("PostgresCollection")]
public class OrderListUserScopeTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-ouscope";
    private const string CountryId = "country-cz-ouscope";

    private const string AccountAId = "user-a-ouscope";
    private const string AccountBId = "user-b-ouscope";
    private const string AccountAEmail = "account-a-ouscope@cleansia.test";
    private const string AccountBEmail = "account-b-ouscope@cleansia.test";

    private const string CleanerUserId = "cleaner-user-ouscope";
    private const string CleanerEmployeeId = "cleaner-emp-ouscope";
    private const string CleanerEmail = "cleaner-ouscope@cleansia.test";

    private const string AccountAFirstOrderId = "order-a1-ouscope";
    private const string AccountASecondOrderId = "order-a2-ouscope";
    private const string AccountBOrderId = "order-b-ouscope";
    private const string GuestWithAEmailOrderId = "order-guest-ouscope";

    [Fact]
    public async Task Admin_UserId_Filter_Returns_That_Accounts_Orders_Only()
    {
        await TestMethod(
            setup: ReplaceWithAdminSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new GetPagedOrders.Request { Filter = EmptyFilter() with { UserId = AccountAId } }),
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                Assert.Equal(2, page.Total);
                Assert.Equal(
                    new[] { AccountAFirstOrderId, AccountASecondOrderId },
                    page.Data!.Select(row => row.Id).OrderBy(id => id));

                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task Admin_UserId_Filter_Leaves_Out_A_Guest_Booking_That_Names_The_Accounts_Email()
    {
        await TestMethod(
            setup: ReplaceWithAdminSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new GetPagedOrders.Request { Filter = EmptyFilter() with { UserId = AccountAId } }),
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                Assert.DoesNotContain(page.Data!, row => row.Id == GuestWithAEmailOrderId);
                Assert.DoesNotContain(page.Data!, row => row.Id == AccountBOrderId);

                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task Admin_Without_A_UserId_Filter_Still_Lists_Every_Order()
    {
        await TestMethod(
            setup: ReplaceWithAdminSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new GetPagedOrders.Request { Filter = EmptyFilter() }),
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                Assert.Equal(4, page.Total);

                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task A_Cleaner_Passing_A_UserId_Is_Not_Narrowed_By_It()
    {
        await TestMethod(
            setup: ReplaceWithCleanerSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new GetPagedOrders.Request
                {
                    Filter = EmptyFilter() with
                    {
                        UserId = AccountBId,
                        IsUnassigned = true,
                        OrderStatuses = new[] { OrderStatus.New, OrderStatus.Confirmed },
                    },
                }),
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                // Every seeded order is takeable, so the board a cleaner sees is the whole set; a
                // UserId that narrowed it to B's single order would be the enumeration the term denies.
                Assert.Equal(4, page.Total);
                Assert.Contains(page.Data!, row => row.Id == AccountAFirstOrderId);
                Assert.Contains(page.Data!, row => row.Id == GuestWithAEmailOrderId);

                return Task.CompletedTask;
            });
    }

    private static OrderFilter EmptyFilter() => new(
        Id: null,
        IsActive: null,
        CustomerName: null,
        CustomerEmail: null,
        CustomerPhone: null,
        DisplayOrderNumber: null,
        EmployeeId: null,
        CleaningDateFrom: null,
        CleaningDateTo: null,
        PaymentStatuses: null,
        PaymentTypes: null,
        MinTotalPrice: null,
        MaxTotalPrice: null,
        OrderStatuses: null,
        HasAvailableSpots: null,
        IsUnassigned: null,
        ExcludeEmployeeId: null);

    private static Task ReplaceWithAdminSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            "admin-ouscope",
            "admin-ouscope@cleansia.test",
            [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));
        return Task.CompletedTask;
    }

    private static Task ReplaceWithCleanerSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            CleanerUserId,
            CleanerEmail,
            [
                new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()),
                new Claim(TestUserSessionProvider.EmployeeIdClaimType, CleanerEmployeeId),
            ])));
        return Task.CompletedTask;
    }

    private static async Task SeedAsync(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        context.CountryConfigurations.Add(CountryConfiguration.Create(CountryId, "CZK", "cs", 0.21m));

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.IsActive = true;
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var accountA = User.CreateWithPassword(AccountAEmail, Constants.TestUserSession.TestUserPassword, "Alena", "Adamova");
        accountA.Id = AccountAId;
        var accountB = User.CreateWithPassword(AccountBEmail, Constants.TestUserSession.TestUserPassword, "Boris", "Benes");
        accountB.Id = AccountBId;
        context.Users.AddRange(accountA, accountB);
        context.Add(CreateApprovedCleaner());

        context.AddRange(
            NewOrder(AccountAFirstOrderId, AccountAEmail, userId: AccountAId),
            NewOrder(AccountASecondOrderId, AccountAEmail, userId: AccountAId),
            NewOrder(AccountBOrderId, AccountBEmail, userId: AccountBId),
            NewOrder(GuestWithAEmailOrderId, AccountAEmail, userId: null));

        await context.CommitAsync(CancellationToken.None);
    }

    private static Employee CreateApprovedCleaner()
    {
        var user = User.CreateWithPassword(CleanerEmail, Constants.TestUserSession.TestUserPassword, "Cyril", "Cerny", UserProfile.Employee);
        user.Id = CleanerUserId;
        user.ConfirmEmail();
        user.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        var employee = Employee.CreateWithUser(user);
        employee.Id = CleanerEmployeeId;
        employee.AssignWorkCountry(CountryId);
        employee.Approve(approvedByUserId: "admin-ouscope");
        employee.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        return employee;
    }

    private static Order NewOrder(string id, string customerEmail, string? userId)
    {
        var order = Order.Create(
            customerName: "Scope Customer",
            customerEmail: customerEmail,
            customerPhone: "+420777111333",
            customerAddress: Address.Create("Scope St 1", "Praha", "12000", CountryId, latitude: 50.0755, longitude: 14.4378),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: userId);
        order.Id = id;
        order.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        return order;
    }
}

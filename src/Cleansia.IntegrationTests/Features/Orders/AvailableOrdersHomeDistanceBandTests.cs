using System.Security.Claims;
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
/// Ruling 5c through the real handler over Postgres: a browsing cleaner's Available row carries a distance
/// band from their STORED home to the job's snapped cell while the row's own coordinates stay null on the
/// wire, and every case without both ends, or with an admin caller, carries none.
///
/// <para>The job sits 3.7 km from the cleaner's home, but the centre of its cell is 2.9 km away, so the
/// expected band (under 3 km) also proves the server measured to the cell, not to the customer's door.</para>
/// </summary>
[Collection("PostgresCollection")]
public class AvailableOrdersHomeDistanceBandTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    // Entity Ids are varchar(26) (ULID length) — every seeded Id must be <= 26 chars.
    private const string CurrencyId = "currency-czk-hdband";
    private const string CountryId = "country-cz-hdband";

    private const string HomeEmployeeId = "employee-home-hdband";
    private const string HomeUserId = "user-home-hdband";
    private const string HomeEmployeeEmail = "home-hdband@cleansia.test";
    private const string NoHomeEmployeeId = "employee-nohome-hdband";
    private const string NoHomeUserId = "user-nohome-hdband";
    private const string NoHomeEmployeeEmail = "nohome-hdband@cleansia.test";

    private const string GeocodedOrderId = "order-geocoded-hdband";
    private const string UngeocodedOrderId = "order-nocoords-hdband";

    private const double HomeLatitude = 50.0755;
    private const double HomeLongitude = 14.4378;
    private const double JobLatitude = 50.042;
    private const double JobLongitude = 14.44;

    [Fact]
    public async Task A_Cleaner_With_A_Home_Reads_The_Band_Of_The_Jobs_Cell_And_No_Coordinates()
    {
        var page = await ReadAvailableAs(services => ReplaceWithEmployeeSession(services, HomeUserId, HomeEmployeeEmail, HomeEmployeeId));

        var row = Assert.Single(page.Data!, r => r.Id == GeocodedOrderId);
        Assert.Equal(HomeDistanceBand.Under3Km, row.HomeDistanceBand);
        Assert.Null(row.CustomerAddressLatitude);
        Assert.Null(row.CustomerAddressLongitude);
    }

    [Fact]
    public async Task A_Cleaner_Whose_Home_Has_No_Coordinates_Reads_No_Band()
    {
        var page = await ReadAvailableAs(services => ReplaceWithEmployeeSession(services, NoHomeUserId, NoHomeEmployeeEmail, NoHomeEmployeeId));

        var row = Assert.Single(page.Data!, r => r.Id == GeocodedOrderId);
        Assert.Null(row.HomeDistanceBand);
    }

    [Fact]
    public async Task An_Order_Not_Yet_Geocoded_Has_No_Band()
    {
        var page = await ReadAvailableAs(services => ReplaceWithEmployeeSession(services, HomeUserId, HomeEmployeeEmail, HomeEmployeeId));

        var row = Assert.Single(page.Data!, r => r.Id == UngeocodedOrderId);
        Assert.Null(row.HomeDistanceBand);
    }

    [Fact]
    public async Task An_Admin_Reads_No_Band()
    {
        var page = await ReadAvailableAs(ReplaceWithAdminSession);

        var row = Assert.Single(page.Data!, r => r.Id == GeocodedOrderId);
        Assert.Null(row.HomeDistanceBand);
        Assert.Equal(JobLatitude, row.CustomerAddressLatitude);
    }

    private async Task<PagedData<OrderListItem>> ReadAvailableAs(Func<IServiceCollection, Task> session)
    {
        PagedData<OrderListItem>? result = null;
        await TestMethod(
            setup: session,
            arrange: Seed,
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new GetPagedOrders.Request
            {
                Filter = new OrderFilter(
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
                    OrderStatuses: [OrderStatus.New, OrderStatus.Confirmed],
                    HasAvailableSpots: null,
                    IsUnassigned: true,
                    ExcludeEmployeeId: null),
            }),
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                result = page;
                return Task.CompletedTask;
            });
        return result!;
    }

    private static async Task Seed(CleansiaDbContext context)
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

        var withHome = CreateApprovedEmployee(HomeUserId, HomeEmployeeId, HomeEmployeeEmail);
        withHome.UpdateAddress(Address.Create(
            "Home St 1", "Praha", "12000", CountryId, latitude: HomeLatitude, longitude: HomeLongitude));
        context.Add(withHome);
        context.Add(CreateApprovedEmployee(NoHomeUserId, NoHomeEmployeeId, NoHomeEmployeeEmail));

        context.Add(CreateOrder(GeocodedOrderId, Address.Create(
            "Job St 2", "Praha", "14000", CountryId, latitude: JobLatitude, longitude: JobLongitude)));
        context.Add(CreateOrder(UngeocodedOrderId, Address.Create("Job St 3", "Praha", "14000", CountryId)));

        await context.CommitAsync(CancellationToken.None);
    }

    private static Employee CreateApprovedEmployee(string userId, string employeeId, string email)
    {
        var user = User.CreateWithPassword(email, Constants.TestUserSession.TestUserPassword, "Eva", "Band", UserProfile.Employee);
        user.Id = userId;
        user.ConfirmEmail();
        user.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        employee.AssignWorkCountry(CountryId);
        employee.Approve(approvedByUserId: "admin-hdband");
        employee.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        return employee;
    }

    private static Order CreateOrder(string orderId, Address address)
    {
        var order = Order.Create(
            customerName: "Band Customer",
            customerEmail: "band-customer@cleansia.test",
            customerPhone: "+420777111222",
            customerAddress: address,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = orderId;
        order.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        return order;
    }

    private static Task ReplaceWithEmployeeSession(IServiceCollection services, string userId, string email, string employeeId)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            userId,
            email,
            [
                new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()),
                new Claim(TestUserSessionProvider.EmployeeIdClaimType, employeeId),
            ])));
        return Task.CompletedTask;
    }

    private static Task ReplaceWithAdminSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            "admin-hdband",
            "admin-hdband@cleansia.test",
            [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));
        return Task.CompletedTask;
    }
}

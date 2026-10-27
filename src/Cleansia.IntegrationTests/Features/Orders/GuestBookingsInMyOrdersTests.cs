using System.Security.Claims;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Features.Orders.Filters;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// Owner ruling 2026-10-26, "I select option B … It can include bookings made before the account existence":
/// a signed-in customer whose address is confirmed sees in My orders every booking made without an account under
/// that address, in any market and whatever the case it was typed in, beside their own. Against real Postgres,
/// through the real pipeline: the listed set is exactly the data subject's set, matched by citext equality on the
/// session's e-mail; a booking under another address, another account's booking that carries the address, and an
/// erased one are not listed; and a cleaner or an administrator signed in with the address lists none.
/// </summary>
[Collection("PostgresCollection")]
public class GuestBookingsInMyOrdersTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string JanaId = "user-jana-listed";
    private const string JanaEmail = "jana.novak@cleansia.test";
    private const string OtherCustomerId = "user-other-listed";
    private const string StaffId = "user-staff-listed";
    private const string CountryId = "country-cz-listed";
    private const string CurrencyId = "cur-czk-listed";
    private const string OwnOrderId = "order-listed-own";
    private const string GuestTypedInCapitalsId = "order-listed-guest-upper";
    private const string GuestInSecondMarketId = "order-listed-guest-second";
    private const string GuestUnderAnotherEmailId = "order-listed-guest-other";
    private const string OtherAccountsOrderId = "order-listed-other-acct";
    private const string ErasedGuestOrderId = "order-listed-anonymised";
    private const string GuestPhone = "+420777000111";
    private const string AccessInstructions = "Code 1234 at the gate.";

    private IUserSessionProvider _session = Customer();

    private Task Setup(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => _session));
        return Task.CompletedTask;
    }

    [Fact]
    public async Task My_Orders_Lists_And_Counts_The_Accounts_Own_Bookings_And_Those_Made_Without_An_Account_Under_Its_Email()
    {
        await TestMethod(
            setup: Setup,
            arrange: SeedAsync,
            act: provider => SendAsync(provider, new GetCustomerOrders.Request()),
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                Assert.Equal(3, page.Total);
                Assert.Equal(
                    [GuestInSecondMarketId, GuestTypedInCapitalsId, OwnOrderId],
                    page.Data.Select(o => o.Id).Order(StringComparer.Ordinal));

                var own = Assert.Single(page.Data, o => o.Id == OwnOrderId);
                Assert.Null(own.IsGuestBooking);
                Assert.NotEmpty(own.CustomerPhone);
                Assert.All(page.Data.Where(o => o.Id != OwnOrderId), guest =>
                {
                    Assert.True(guest.IsGuestBooking);
                    Assert.Empty(guest.CustomerPhone);
                    Assert.NotEmpty(guest.CustomerName);
                    Assert.NotEmpty(guest.CustomerAddress);
                });
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Listed_Set_Is_The_Data_Subjects_Set_Found_By_Citext_Equality_On_The_Email()
    {
        await TestMethod(
            setup: Setup,
            arrange: SeedAsync,
            act: provider =>
            {
                var listed = provider.GetRequiredService<IOrderRepository>().GetQueryableListedForCustomer(JanaId, JanaEmail);
                var subjects = provider.GetRequiredService<CleansiaDbContext>().Orders.IgnoreQueryFilters()
                    .Where(SubjectOrders.Of(JanaId, JanaEmail));
                return Task.FromResult((
                    Sql: listed.ToQueryString(),
                    Listed: listed.Select(o => o.Id).ToList().Order(StringComparer.Ordinal).ToList(),
                    Subjects: subjects.Select(o => o.Id).ToList().Order(StringComparer.Ordinal).ToList()));
            },
            assert: (CleansiaDbContext _, (string Sql, List<string> Listed, List<string> Subjects) read) =>
            {
                Assert.Equal([GuestInSecondMarketId, GuestTypedInCapitalsId, OwnOrderId], read.Listed);
                Assert.Equal(read.Subjects, read.Listed);
                Assert.DoesNotContain("lower(", read.Sql, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("\"CustomerEmail\" = @", read.Sql, StringComparison.Ordinal);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Filter_Applies_To_The_Guest_Bookings_As_To_The_Accounts_Own()
    {
        await TestMethod(
            setup: Setup,
            arrange: SeedAsync,
            act: provider => SendAsync(provider, new GetCustomerOrders.Request
            {
                Filter = new OrderFilter(
                    Id: null, IsActive: null, CustomerName: null, CustomerEmail: null, CustomerPhone: null,
                    DisplayOrderNumber: null, EmployeeId: null, CleaningDateFrom: null, CleaningDateTo: null,
                    PaymentStatuses: null, PaymentTypes: null, MinTotalPrice: null, MaxTotalPrice: null,
                    OrderStatuses: [OrderStatus.Completed], HasAvailableSpots: null, IsUnassigned: null,
                    ExcludeEmployeeId: null),
            }),
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                Assert.Equal(1, page.Total);
                Assert.Equal(GuestInSecondMarketId, Assert.Single(page.Data).Id);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Theory]
    [InlineData(UserProfile.Employee)]
    [InlineData(UserProfile.Administrator)]
    public async Task Staff_Signed_In_To_A_Customer_Host_With_The_Address_List_No_Booking_Made_Under_It(UserProfile profile)
    {
        _session = new TestUserSessionProvider(StaffId, JanaEmail, [new Claim(ClaimTypes.Role, profile.ToString())]);

        await TestMethod(
            setup: Setup,
            arrange: SeedAsync,
            act: provider => SendAsync(provider, new GetCustomerOrders.Request()),
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                Assert.Equal(0, page.Total);
                Assert.Empty(page.Data);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Theory]
    [InlineData(GuestTypedInCapitalsId)]
    [InlineData(GuestInSecondMarketId)]
    public async Task A_Listed_Guest_Booking_Opens_With_What_Its_Emails_Carried_And_Nothing_The_Household_Wrote(string orderId)
    {
        await TestMethod(
            setup: Setup,
            arrange: SeedAsync,
            act: provider => SendAsync(provider, new GetOrderDetails.Query(orderId)),
            assert: (CleansiaDbContext _, BusinessResult<OrderItem> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                var detail = result.Value!;
                Assert.True(detail.IsGuestBooking);
                Assert.Equal("Jana Nováková", detail.CustomerName);
                Assert.Equal("Vinohradská 12", detail.Address!.Street);
                Assert.NotNull(detail.FreeCancellationHours);
                Assert.Empty(detail.CustomerPhone);
                Assert.Null(detail.AccessInstructions);
                Assert.Null(detail.HasAccessInstructions);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Accounts_Own_Booking_Opens_As_Before()
    {
        await TestMethod(
            setup: Setup,
            arrange: SeedAsync,
            act: provider => SendAsync(provider, new GetOrderDetails.Query(OwnOrderId)),
            assert: (CleansiaDbContext _, BusinessResult<OrderItem> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Null(result.Value!.IsGuestBooking);
                Assert.Equal(GuestPhone, result.Value.CustomerPhone);
                Assert.Equal(AccessInstructions, result.Value.AccessInstructions);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Theory]
    [InlineData(GuestUnderAnotherEmailId)]
    [InlineData(OtherAccountsOrderId)]
    [InlineData(ErasedGuestOrderId)]
    public async Task A_Booking_The_Account_Is_Not_Listed_For_Is_Not_Found(string orderId)
    {
        await TestMethod(
            setup: Setup,
            arrange: SeedAsync,
            act: provider => SendAsync(provider, new GetOrderDetails.Query(orderId)),
            assert: (CleansiaDbContext _, BusinessResult<OrderItem> result) =>
            {
                Assert.True(result.IsFailure);
                Assert.Contains(Assert.IsAssignableFrom<IValidationResult>(result).Errors,
                    e => e.Message == BusinessErrorMessage.OrderNotFound);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    private static IUserSessionProvider Customer() =>
        new TestUserSessionProvider(JanaId, JanaEmail, [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())]);

    private static async Task<TResponse> SendAsync<TResponse>(IServiceProvider provider, IRequest<TResponse> request)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    /// <summary>
    /// Jana's account and her own booking; two bookings made without an account under her address, one typed in
    /// capitals a month before the account existed and one in the second operator's market; and three that are
    /// not hers to list: one under another address, another account's booking carrying hers, and an erased one.
    /// </summary>
    private static async Task SeedAsync(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var jana = User.CreateWithPassword(JanaEmail, "Seed-Password-123", "Jana", "Nováková");
        jana.Id = JanaId;
        jana.ConfirmEmail();
        var other = User.CreateWithPassword("petr.svoboda@cleansia.test", "Seed-Password-123", "Petr", "Svoboda");
        other.Id = OtherCustomerId;
        other.ConfirmEmail();
        context.Users.AddRange(jana, other);
        StampUnstampedAdded(context, TestTenants.Default);

        context.Orders.Add(NewOrder(GuestInSecondMarketId, userId: null, JanaEmail, OrderStatus.Completed));
        StampUnstampedAdded(context, TestTenants.Second);

        var erased = NewOrder(ErasedGuestOrderId, userId: null, JanaEmail, OrderStatus.Completed);
        erased.AnonymizeCustomerData();
        context.Orders.AddRange(
            NewOrder(OwnOrderId, JanaId, JanaEmail, OrderStatus.New),
            NewOrder(GuestTypedInCapitalsId, userId: null, JanaEmail.ToUpperInvariant(), OrderStatus.New,
                bookedOn: DateTimeOffset.UtcNow.AddDays(-30)),
            NewOrder(GuestUnderAnotherEmailId, userId: null, "someone.else@cleansia.test", OrderStatus.New),
            NewOrder(OtherAccountsOrderId, OtherCustomerId, JanaEmail, OrderStatus.New),
            erased);
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    private static Order NewOrder(
        string id, string? userId, string customerEmail, OrderStatus status, DateTimeOffset? bookedOn = null)
    {
        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: customerEmail,
            customerPhone: GuestPhone,
            customerAddress: Address.Create("Vinohradská 12", "Praha", "12000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: userId,
            accessInstructions: AccessInstructions,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = id;
        order.Created("seed", bookedOn ?? DateTimeOffset.UtcNow.AddDays(-2));
        order.AssignStripePaymentIntentId($"pi_{id}");
        var stamp = (bookedOn ?? DateTimeOffset.UtcNow.AddDays(-2)).AddMinutes(1);
        foreach (var track in status == OrderStatus.New ? [OrderStatus.New] : new[] { OrderStatus.New, status })
        {
            var entry = OrderStatusTrack.Create(track, order);
            entry.Created("seed", stamp);
            order.AddOrderStatus(entry);
            stamp = stamp.AddMinutes(1);
        }

        return order;
    }
}

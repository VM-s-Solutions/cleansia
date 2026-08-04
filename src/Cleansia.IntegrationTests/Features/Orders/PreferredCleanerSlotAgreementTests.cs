using System.Security.Claims;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// ADR-0039 AC2, over REAL Postgres — <b>the property that is worth more than either answer alone.</b>
///
/// <para>The picker tells the customer who they may choose; the hold resolver decides, seconds later,
/// whether that choice earns anything. If the picker can say <i>available</i> and the resolver can then
/// say <i>busy</i> for a reason of its own, the feature has already failed and no amount of shared
/// documentation prevents it. So the two do not share "the same rule" — they share the same repository
/// method and the same window, and this test is what says so out loud.</para>
///
/// <para>The fixture is chosen so a half-open/closed mistake in the interval would break agreement
/// rather than hide: one cleaner overlaps the slot, one is committed right up to the instant it starts,
/// and one has a terminal order sitting in the middle of it.</para>
/// </summary>
[Collection("PostgresCollection")]
public class PreferredCleanerSlotAgreementTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "cur-czk-slot";
    private const string CountryId = "ctry-cz-slot";
    private const string CustomerId = "user-slot-cust";
    private const string ServiceId = "svc-slot";
    private const int ServiceMinutes = 180;

    private const string BusyCleaner = "emp-slot-busy";
    private const string AdjacentCleaner = "emp-slot-adj";
    private const string TerminalCleaner = "emp-slot-term";

    private static readonly DateTime CleaningUtc = new(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = CleaningUtc.AddDays(-4);

    /// <summary>Expected verdict per cleaner: is the platform able to offer them this slot?</summary>
    private static readonly (string EmployeeId, bool Available)[] Cases =
    [
        (BusyCleaner, false),
        (AdjacentCleaner, true),
        (TerminalCleaner, true),
    ];

    [Fact]
    public async Task The_Picker_And_The_Hold_Resolver_Give_The_Same_Answer_Per_Cleaner()
    {
        await TestMethod(
            setup: ReplaceWithCustomerSession,
            arrange: SeedTheSlotMatrix,
            act: async provider =>
            {
                var picker = await provider.GetRequiredService<IMediator>().Send(
                    new GetMyServingCleaners.Query(
                        CleaningDateTimeUtc: CleaningUtc,
                        SelectedServiceIds: [ServiceId],
                        SelectedPackageIds: []));

                var resolver = provider.GetRequiredService<IPreferredCleanerHoldResolver>();
                var outcomes = new Dictionary<string, PreferredCleanerOutcome>();
                foreach (var (employeeId, _) in Cases)
                {
                    outcomes[employeeId] = await resolver.ResolveAsync(
                        CustomerId, employeeId, CountryId, CleaningUtc, ServiceMinutes, Now, CancellationToken.None);
                }

                return (Picker: picker.Value!, Resolver: outcomes);
            },
            assert: (CleansiaDbContext _,
                    (IReadOnlyList<GetMyServingCleaners.Response> Picker, Dictionary<string, PreferredCleanerOutcome> Resolver) result) =>
            {
                var offered = result.Picker.ToDictionary(r => r.EmployeeId);
                Assert.Equal(Cases.Length, offered.Count);

                foreach (var (employeeId, available) in Cases)
                {
                    var pickerSaidAvailable = offered[employeeId].IsAvailableForRequestedSlot;
                    var resolverSaidBusy =
                        result.Resolver[employeeId].Reason == HoldDeclineReason.CleanerBusyAtCleaningTime;

                    Assert.Equal(available, pickerSaidAvailable);
                    Assert.Equal(available, !resolverSaidBusy);
                }

                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// The consequence the customer sees: the cleaner the picker offered earns the exclusive hold, and
    /// the one it greyed out earns neither the hold nor the targeted push.
    /// </summary>
    [Fact]
    public async Task Only_The_Cleaner_The_Picker_Offered_Earns_The_Hold()
    {
        await TestMethod(
            setup: ReplaceWithCustomerSession,
            arrange: SeedTheSlotMatrix,
            act: async provider =>
            {
                var resolver = provider.GetRequiredService<IPreferredCleanerHoldResolver>();
                return (
                    Busy: await resolver.ResolveAsync(
                        CustomerId, BusyCleaner, CountryId, CleaningUtc, ServiceMinutes, Now, CancellationToken.None),
                    Free: await resolver.ResolveAsync(
                        CustomerId, AdjacentCleaner, CountryId, CleaningUtc, ServiceMinutes, Now, CancellationToken.None));
            },
            assert: (CleansiaDbContext _, (PreferredCleanerOutcome Busy, PreferredCleanerOutcome Free) result) =>
            {
                Assert.False(result.Busy.NotifyPreferred);
                Assert.Null(result.Busy.HoldUntilUtc);
                Assert.Equal(HoldDeclineReason.CleanerBusyAtCleaningTime, result.Busy.Reason);

                Assert.True(result.Free.NotifyPreferred);
                Assert.Equal(HoldDeclineReason.None, result.Free.Reason);
                Assert.Equal(Now.Add(BookingPolicy.ComputePreferredHold(CleaningUtc, Now)), result.Free.HoldUntilUtc);

                return Task.CompletedTask;
            });
    }

    private static Task ReplaceWithCustomerSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            CustomerId,
            "slot-agreement@cleansia.test",
            [new Claim(ClaimTypes.NameIdentifier, CustomerId)])));
        return Task.CompletedTask;
    }

    private static async Task SeedTheSlotMatrix(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var category = ServiceCategory.Create("home", "Home", "Home cleaning");
        category.Id = "cat-slot";
        context.Add(category);

        var service = Service.Create(category.Id, "Deep clean", "Deep clean", 1500m, 200m, ServiceMinutes);
        service.Id = ServiceId;
        context.Add(service);

        var customer = User.CreateWithPassword(
            "slot-agreement@cleansia.test",
            TestUtilities.Constants.TestUserSession.TestUserPassword,
            "Clara",
            "Customer",
            UserProfile.Customer);
        customer.Id = CustomerId;
        customer.ConfirmEmail();
        customer.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        context.Add(customer);

        context.Add(UserMembership.Create(
            userId: CustomerId,
            membershipPlanId: NewPlan(context).Id,
            stripeSubscriptionId: "sub_slot_agreement",
            currentPeriodStart: Now.AddDays(-10),
            currentPeriodEnd: Now.AddDays(20)));

        var slotEnd = CleaningUtc.AddMinutes(ServiceMinutes);

        // Overlaps the middle of the slot.
        SeedCleanerWithCommitment(context, BusyCleaner, "Bea", "Busy",
            commitmentStart: CleaningUtc.AddMinutes(60), commitmentMinutes: 60, status: OrderStatus.Confirmed);

        // Ends exactly when the slot opens — a closed lower bound would wrongly call this a conflict.
        SeedCleanerWithCommitment(context, AdjacentCleaner, "Ada", "Adjacent",
            commitmentStart: CleaningUtc.AddMinutes(-120), commitmentMinutes: 120, status: OrderStatus.Confirmed);

        // Sits in the middle of the slot but is terminal, so it hands the calendar back.
        SeedCleanerWithCommitment(context, TerminalCleaner, "Tea", "Terminal",
            commitmentStart: CleaningUtc.AddMinutes(30), commitmentMinutes: 60, status: OrderStatus.Cancelled);

        Assert.True(slotEnd > CleaningUtc);
        await context.CommitAsync(CancellationToken.None);
    }

    private static MembershipPlan NewPlan(CleansiaDbContext context)
    {
        var plan = MembershipPlan.Create(
            code: "PLUS",
            name: "Cleansia Plus",
            monthlyPriceCzk: 299m,
            stripePriceId: "price_slot_agreement",
            discountPercentage: 5m,
            freeCancellationWindowHours: 48,
            allowsExpressUpgrade: true);
        plan.Id = "plan-slot";
        context.Add(plan);
        return plan;
    }

    /// <summary>
    /// Each cleaner gets a completed job for this customer (which is what puts them in the picker's
    /// set), a reachable device, and one commitment whose overlap with the slot is the case under test.
    /// </summary>
    private static void SeedCleanerWithCommitment(
        CleansiaDbContext context,
        string employeeId,
        string firstName,
        string lastName,
        DateTime commitmentStart,
        int commitmentMinutes,
        OrderStatus status)
    {
        var user = User.CreateWithPassword(
            $"{employeeId}@cleansia.test",
            TestUtilities.Constants.TestUserSession.TestUserPassword,
            firstName,
            lastName,
            UserProfile.Employee);
        user.Id = $"user-{employeeId}";
        user.ConfirmEmail();
        user.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        employee.Approve(approvedByUserId: "admin-slot");
        employee.AssignWorkCountry(CountryId);
        employee.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        context.Add(employee);

        var device = Device.Create(user.Id, "ios", $"token-{employeeId}", $"device-{employeeId}");
        device.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        context.Add(device);

        var history = NewOrder(
            $"h-{employeeId}", Now.AddDays(-30), 120, OrderStatus.Completed, ownedByCustomer: true);
        history.AddAssignedEmployee(OrderEmployee.Create(history, employee));
        context.Add(history);

        var commitment = NewOrder(
            $"c-{employeeId}", commitmentStart, commitmentMinutes, status, ownedByCustomer: false);
        commitment.AddAssignedEmployee(OrderEmployee.Create(commitment, employee));
        context.Add(commitment);
    }

    private static Order NewOrder(
        string orderId, DateTime cleaningDateTime, int estimatedMinutes, OrderStatus status, bool ownedByCustomer)
    {
        var order = Order.Create(
            customerName: "Slot Customer",
            customerEmail: "slot-agreement@cleansia.test",
            customerPhone: "+420777888999",
            customerAddress: Address.Create("Slot St 3", "Brno", "60200", CountryId),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: ownedByCustomer ? CustomerId : null);
        order.Id = orderId;
        order.UpdateEstimatedTime(estimatedMinutes);
        order.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        if (status != OrderStatus.New)
        {
            order.AddOrderStatus(OrderStatusTrack.Create(status, order));
        }

        return order;
    }
}

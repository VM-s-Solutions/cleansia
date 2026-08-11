using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// ADR-0045 D2, the load-bearing decision, against the SHIPPED queries over real Postgres:
///
/// <blockquote>A customer's preference may spend the platform's fill window. It may never spend a
/// cleaner's CAPACITY.</blockquote>
///
/// <para>The obvious implementation of "he has to be assigned" is an <c>OrderEmployee</c> row at
/// booking with a confirmation state on it. It is rejected on three pieces of shipped code, and these
/// are those three pieces — asserted here rather than argued, because each is a query written for a
/// different purpose that would silently start counting a reservation the day somebody added the
/// row:</para>
/// <list type="number">
///   <item><c>GetEmployeeOrderCountThisWeekAsync</c> has no status term and no confirmation term, so a
///   row would spend the cleaner's rating-tiered 3/6/10 weekly cap on a job they never agreed to —
///   three unanswered offers in a morning would exhaust a 3-cap cleaner's whole week.</item>
///   <item><c>LiveCommitmentsInWindow</c> is the ONE overlap predicate, read by both the TakeOrder
///   conflict gate and ADR-0039's picker, so a row would mark the cleaner busy for a window they have
///   not accepted — against jobs they would have taken.</item>
///   <item>the cancellation fee (its own unit test — <c>ReservationIsNotAcceptanceTests</c>) keys on the
///   same assignment count.</item>
/// </list>
///
/// <para>Undoing any of it would require teaching the occupancy predicate a confirmation term — a
/// SECOND definition of "occupied", which fails in the direction that double-books a cleaner standing
/// in someone's flat.</para>
/// </summary>
[Collection("PostgresCollection")]
public class ReservationSpendsNoCapacityTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-cap";
    private const string CountryId = "country-cz-cap";
    private const string BeneficiaryId = "employee-cap-beneficiary";
    private const string BeneficiaryUserId = "user-cap-beneficiary";

    /// <summary>Inside the current UTC week, which is what the weekly-cap query bounds on.</summary>
    private static readonly DateTime ReservedSlot = StartOfThisWeekUtc().AddDays(2).AddHours(9);

    [Fact]
    public async Task A_Reservation_Does_Not_Spend_The_Cleaners_Weekly_Cap()
    {
        await TestMethod(
            arrange: SeedOneReservedOrder,
            act: async provider => await provider
                .GetRequiredService<IOrderRepository>()
                .GetEmployeeOrderCountThisWeekAsync(BeneficiaryId, CancellationToken.None),
            assert: (CleansiaDbContext _, int weeklyCount) =>
            {
                Assert.Equal(0, weeklyCount);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task A_Reservation_Does_Not_Block_The_Cleaners_Calendar()
    {
        await TestMethod(
            arrange: SeedOneReservedOrder,
            act: async provider =>
            {
                var orders = provider.GetRequiredService<IOrderRepository>();

                var overlaps = await orders.HasOverlappingOrderAsync(
                    BeneficiaryId, ReservedSlot, 120, CancellationToken.None);
                var busy = await orders.GetBusyEmployeeIdsInWindowAsync(
                    [BeneficiaryId], ReservedSlot, ReservedSlot.AddMinutes(120), CancellationToken.None);

                return (Overlaps: overlaps, Busy: busy.Contains(BeneficiaryId));
            },
            assert: (CleansiaDbContext _, (bool Overlaps, bool Busy) verdicts) =>
            {
                Assert.False(verdicts.Overlaps);
                Assert.False(verdicts.Busy);
                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// The control: the very same order, the very same window, once the cleaner has actually COMMITTED.
    /// Without this the two assertions above pass on a fixture that could not have blocked anything —
    /// a wrong date, an unseeded row, a mis-typed employee id — and would prove nothing at all.
    /// </summary>
    [Fact]
    public async Task An_Assignment_On_The_Same_Order_Spends_Both()
    {
        await TestMethod(
            arrange: async context =>
            {
                await SeedOneReservedOrder(context);

                var order = await context.Orders.FindAsync("order-cap-reserved");
                var beneficiary = await context.Set<Employee>().FindAsync(BeneficiaryId);
                order!.AddAssignedEmployee(OrderEmployee.Create(order, beneficiary!));
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var orders = provider.GetRequiredService<IOrderRepository>();

                var weeklyCount = await orders.GetEmployeeOrderCountThisWeekAsync(
                    BeneficiaryId, CancellationToken.None);
                var overlaps = await orders.HasOverlappingOrderAsync(
                    BeneficiaryId, ReservedSlot, 120, CancellationToken.None);
                var busy = await orders.GetBusyEmployeeIdsInWindowAsync(
                    [BeneficiaryId], ReservedSlot, ReservedSlot.AddMinutes(120), CancellationToken.None);

                return (WeeklyCount: weeklyCount, Overlaps: overlaps, Busy: busy.Contains(BeneficiaryId));
            },
            assert: (CleansiaDbContext _, (int WeeklyCount, bool Overlaps, bool Busy) verdicts) =>
            {
                Assert.Equal(1, verdicts.WeeklyCount);
                Assert.True(verdicts.Overlaps);
                Assert.True(verdicts.Busy);
                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// ADR-0045 D1.1's premise, over real Postgres: the reservation is findable by beneficiary + window
    /// through a fourth terminal shape on the SAME window filter the two queries above use — never a
    /// second overlap predicate.
    /// </summary>
    [Fact]
    public async Task The_Reservation_Is_Still_Findable_By_Beneficiary_And_Window()
    {
        await TestMethod(
            arrange: SeedOneReservedOrder,
            act: async provider => await provider
                .GetRequiredService<IOrderRepository>()
                .GetLiveReservationsForBeneficiaryInWindowAsync(
                    BeneficiaryId,
                    ReservedSlot,
                    ReservedSlot.AddMinutes(120),
                    DateTime.UtcNow,
                    CancellationToken.None),
            assert: (CleansiaDbContext _, IReadOnlyList<Order> found) =>
            {
                Assert.Equal("order-cap-reserved", Assert.Single(found).Id);
                return Task.CompletedTask;
            });
    }

    private static async Task SeedOneReservedOrder(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var user = User.CreateWithPassword(
            "cleaner-cap@cleansia.test",
            TestUtilities.Constants.TestUserSession.TestUserPassword,
            "Bea",
            "Beneficiary",
            UserProfile.Employee);
        user.Id = BeneficiaryUserId;
        user.ConfirmEmail();
        user.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        var beneficiary = Employee.CreateWithUser(user);
        beneficiary.Id = BeneficiaryId;
        beneficiary.Approve(approvedByUserId: "admin-cap");
        beneficiary.AssignWorkCountry(CountryId);
        beneficiary.UpdateAddress(Address.Create("Cleaner St 3", "Brno", "60200", CountryId));
        beneficiary.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        context.Add(beneficiary);

        var order = Order.Create(
            customerName: "Capacity Customer",
            customerEmail: "capacity@cleansia.test",
            customerPhone: "+420777222333",
            customerAddress: Address.Create("Reserved St 1", "Brno", "60200", CountryId),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: ReservedSlot,
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            preferredEmployeeId: BeneficiaryId);
        order.Id = "order-cap-reserved";
        order.UpdateEstimatedTime(120);
        order.SetMaxEmployees(2);
        order.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        order.GrantPreferredHold(
            BeneficiaryId,
            DateTime.UtcNow.AddHours(3),
            DateTime.UtcNow,
            BookingPolicy.MaxPreferredOfferRounds);
        context.Add(order);

        await context.CommitAsync(CancellationToken.None);
    }

    private static DateTime StartOfThisWeekUtc()
    {
        var today = DateTime.UtcNow.Date;
        return today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
    }
}

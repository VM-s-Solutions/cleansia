using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
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
/// ADR-0045 D9 — the cleaner-facing surface, over real Postgres because its four terms are SQL and
/// because <c>OrderAvailability.IsOfferableSql</c> is CONJOINED here rather than extended: SQL and C#
/// disagree about nulls, so an in-memory rehearsal would prove the wrong thing.
///
/// <para>Each row below is offerable, seat-free and Confirmed unless the case's own term says otherwise,
/// so the ONE thing that can separate the verdicts is the term under test. The offerability case is the
/// one this surface must not inherit from <c>CanBrowseOrderAsync</c>: a <c>New</c> + Card order whose
/// money has not landed is refused by the take gate and may be cancelled within the hour, and under a
/// DISCLOSED reservation that reads as "you were assigned a job that vanished".</para>
/// </summary>
[Collection("PostgresCollection")]
public class PendingOffersSurfaceTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-offers";
    private const string CountryId = "country-cz-offers";
    private const string CallerEmployeeId = "employee-offers-caller";
    private const string CallerUserId = "user-offers-caller";
    private const string CallerEmail = "cleaner-offers@cleansia.test";
    private const string RivalEmployeeId = "employee-offers-rival";

    private static readonly DateTime Live = DateTime.UtcNow.AddHours(3);
    private static readonly DateTime Lapsed = DateTime.UtcNow.AddMinutes(-1);

    private sealed record OfferCase(
        string OrderId,
        string? Beneficiary,
        DateTime? HoldUntilUtc,
        bool Listed,
        OrderStatus CurrentStatus = OrderStatus.Confirmed,
        PaymentStatus PaymentStatus = PaymentStatus.Paid,
        bool Filled = false);

    private static readonly OfferCase[] Cases =
    [
        new("offer-mine-live", CallerEmployeeId, Live, Listed: true),
        new("offer-mine-lapsed", CallerEmployeeId, Lapsed, Listed: false),
        new("offer-someone-elses", RivalEmployeeId, Live, Listed: false),
        new("offer-no-reservation", null, null, Listed: false),
        new("offer-mine-but-full", CallerEmployeeId, Live, Listed: false, Filled: true),
        new(
            "offer-mine-money-pending",
            CallerEmployeeId,
            Live,
            Listed: false,
            CurrentStatus: OrderStatus.New,
            PaymentStatus: PaymentStatus.Pending),
        new(
            "offer-mine-cancelled",
            CallerEmployeeId,
            Live,
            Listed: false,
            CurrentStatus: OrderStatus.Cancelled),
    ];

    [Fact]
    public async Task Only_Live_Offerable_Unfilled_Reservations_For_This_Cleaner_Are_Listed()
    {
        await TestMethod(
            setup: ReplaceWithCallerSession,
            arrange: SeedTheOfferMatrix,
            act: async provider => await provider
                .GetRequiredService<IMediator>()
                .Send(new GetMyPendingOffers.Query()),
            assert: (CleansiaDbContext _, Cleansia.Infra.Common.Validations.BusinessResult<IReadOnlyList<PendingOfferItem>> result) =>
            {
                Assert.True(result.IsSuccess);
                var listed = result.Value!.Select(o => o.Id).ToHashSet();

                foreach (var scenario in Cases)
                {
                    Assert.Equal(scenario.Listed, listed.Contains(scenario.OrderId));
                }

                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// The deadline is the whole reason this surface is not the ordinary board, and it is the same
    /// instant the customer's own order detail shows — so the two sides never see different deadlines.
    /// The coarse address is the pre-acceptance one; the cleaner has not accepted yet.
    /// </summary>
    [Fact]
    public async Task A_Listed_Offer_Carries_Its_Deadline_And_Only_The_Coarse_Address()
    {
        await TestMethod(
            setup: ReplaceWithCallerSession,
            arrange: SeedTheOfferMatrix,
            act: async provider => await provider
                .GetRequiredService<IMediator>()
                .Send(new GetMyPendingOffers.Query()),
            assert: (CleansiaDbContext _, Cleansia.Infra.Common.Validations.BusinessResult<IReadOnlyList<PendingOfferItem>> result) =>
            {
                var offer = Assert.Single(result.Value!);

                Assert.Equal(Live, offer.RespondByUtc, TimeSpan.FromSeconds(1));
                Assert.Equal("Brno · 602", offer.CustomerAddressApproximate);
                Assert.Equal("CZK", offer.CurrencyCode);

                // Over the SERIALIZED row, not over the coarse field: the seeded street is real
                // ("Held St 7"), and asserting a coarse string does not contain it is true by
                // construction and blind to any member added later. This one reddens for a new member
                // that carries the street, whatever it is called.
                Assert.DoesNotContain("Held St", JsonSerializer.Serialize(offer));

                return Task.CompletedTask;
            });
    }

    private static Task ReplaceWithCallerSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            CallerUserId,
            CallerEmail,
            [
                new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()),
                new Claim(TestUserSessionProvider.EmployeeIdClaimType, CallerEmployeeId),
            ])));
        return Task.CompletedTask;
    }

    private static async Task SeedTheOfferMatrix(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var caller = NewCleaner(CallerEmployeeId, CallerUserId, CallerEmail, "Petra", "Caller");
        var rival = NewCleaner(
            RivalEmployeeId, "user-offers-rival", "cleaner-offers-rival@cleansia.test", "Rita", "Rival");
        context.Add(caller);
        context.Add(rival);

        var slot = 0;
        foreach (var scenario in Cases)
        {
            var order = NewOrder(scenario, DateTime.UtcNow.AddDays(2).AddHours(slot * 8));
            if (scenario.Filled)
            {
                order.AddAssignedEmployee(OrderEmployee.Create(order, rival));
            }

            context.Add(order);
            slot++;
        }

        await context.CommitAsync(CancellationToken.None);
    }

    private static Order NewOrder(OfferCase scenario, DateTime cleaningDateTime)
    {
        var order = Order.Create(
            customerName: "Offer Customer",
            customerEmail: "offer-customer@cleansia.test",
            customerPhone: "+420777111555",
            customerAddress: Address.Create("Held St 7", "Brno", "60200", CountryId),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: scenario.PaymentStatus,
            preferredEmployeeId: scenario.Beneficiary);
        order.Id = scenario.OrderId;
        order.UpdateEstimatedTime(120);
        order.SetMaxEmployees(1);
        order.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        if (scenario.CurrentStatus != OrderStatus.New)
        {
            order.AddOrderStatus(OrderStatusTrack.Create(scenario.CurrentStatus, order));
        }

        if (scenario.HoldUntilUtc is { } until && scenario.Beneficiary is not null)
        {
            order.GrantPreferredHold(
                scenario.Beneficiary, until, until.AddHours(-2), BookingPolicy.MaxPreferredOfferRounds);
        }

        return order;
    }

    private static Employee NewCleaner(
        string employeeId, string userId, string email, string firstName, string lastName)
    {
        var user = User.CreateWithPassword(
            email,
            TestUtilities.Constants.TestUserSession.TestUserPassword,
            firstName,
            lastName,
            UserProfile.Employee);
        user.Id = userId;
        user.ConfirmEmail();
        user.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        employee.Approve(approvedByUserId: "admin-offers");
        employee.AssignWorkCountry(CountryId);
        employee.UpdateAddress(Address.Create("Cleaner St 9", "Brno", "60200", CountryId));
        employee.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        return employee;
    }
}

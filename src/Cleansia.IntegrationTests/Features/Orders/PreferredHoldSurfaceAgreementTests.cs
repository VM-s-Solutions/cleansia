using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Dashboard;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Features.Orders.Filters;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// TC-PREF-HOLD-0 / EXPIRE-0 / CONSUMED-0 / NULLPAIR-0 / LEGACY-0 / TAKE-0, as ONE assertion (ADR-0036
/// D5), over REAL Postgres.
///
/// <para>The hold is written once and read by four independent decision points — the paged board's
/// server floor, the dashboard's available count, the browse authorization behind order detail and
/// photos, and the take gate. Testing each alone proves nothing that matters: the failure this rule
/// exists to prevent is not "a surface is wrong", it is "two surfaces disagree". A hold enforced on the
/// board but not at the take gate hides a job from the cleaner it belongs to while still handing it to
/// whoever guesses the id; a hold enforced at the take gate but not on the board advertises a job that
/// refuses every taker. So the fixture is walked once and all four verdicts are compared per order.</para>
///
/// <para>Every row is seeded <c>Confirmed</c> + <c>Card</c> + <c>Paid</c> with a free seat and — bar the
/// consumed case — no assignee, so the offerability and seat terms admit all of them and the ONLY thing
/// that can separate the verdicts is the hold. A <c>New</c> fixture would have made the count and
/// preview assertions pass before the rule was written.</para>
/// </summary>
[Collection("PostgresCollection")]
public class PreferredHoldSurfaceAgreementTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-pref";
    private const string CountryId = "country-cz-pref";
    private const string CallerEmployeeId = "employee-pref-caller";
    private const string CallerUserId = "user-pref-caller";
    private const string CallerEmail = "cleaner-pref@cleansia.test";
    private const string RivalEmployeeId = "employee-pref-rival";
    private const string ThirdEmployeeId = "employee-pref-third";

    private static readonly DateTime Past = DateTime.UtcNow.AddHours(-1);
    private static readonly DateTime Future = DateTime.UtcNow.AddHours(3);

    /// <summary>
    /// One row per term of the rule, read from the CALLER's point of view. Exactly one is withheld —
    /// which is the point: four of these six "open" verdicts are open for a different reason, and a
    /// predicate missing any one term turns its row red on all four surfaces at once.
    /// </summary>
    private static readonly HoldCase[] Cases =
    [
        new("pref-no-hold", null, null, null, true),
        new("pref-legacy-pref", null, RivalEmployeeId, null, true),
        new("pref-held-rival", Future, RivalEmployeeId, null, false),
        new("pref-held-mine", Future, CallerEmployeeId, null, true),
        new("pref-expired", Past, RivalEmployeeId, null, true),
        new("pref-stranded", Future, null, null, true),
        new("pref-consumed", Future, RivalEmployeeId, ThirdEmployeeId, true),
    ];

    [Fact]
    public async Task The_Board_The_Count_The_Browse_Gate_And_The_Take_Gate_Agree_Order_By_Order()
    {
        await TestMethod(
            setup: ReplaceWithCallerSession,
            arrange: SeedTheHoldMatrix,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var board = await mediator.Send(AvailableRequest());

                var orderRepository = provider.GetRequiredService<IOrderRepository>();
                var counted = await orderRepository
                    .GetQueryable()
                    .Where(DashboardSpecifications
                        .CreateAvailableOrdersSpec(CallerEmployeeId, DateTime.UtcNow)
                        .SatisfiedBy())
                    .Select(o => o.Id)
                    .ToListAsync();

                var accessService = provider.GetRequiredService<IOrderAccessService>();
                var validator = new TakeOrder.Validator(
                    orderRepository,
                    provider.GetRequiredService<IEmployeeRepository>(),
                    accessService);

                var browsable = new Dictionary<string, bool>();
                var takeVerdicts = new Dictionary<string, string?>();
                foreach (var scenario in Cases)
                {
                    var order = await orderRepository.GetByIdAsync(scenario.OrderId, CancellationToken.None);
                    browsable[scenario.OrderId] =
                        await accessService.CanBrowseOrderAsync(order!, CancellationToken.None);

                    var result = await validator.ValidateAsync(new TakeOrder.Command(scenario.OrderId));
                    takeVerdicts[scenario.OrderId] = result.IsValid
                        ? null
                        : Assert.Single(result.Errors).ErrorMessage;
                }

                return new Verdicts(board, counted, browsable, takeVerdicts);
            },
            assert: (CleansiaDbContext _, Verdicts verdicts) =>
            {
                var listed = verdicts.Board.Data!.Select(o => o.Id).ToHashSet();

                foreach (var scenario in Cases)
                {
                    Assert.Equal(scenario.Open, listed.Contains(scenario.OrderId));
                    Assert.Equal(scenario.Open, verdicts.Counted.Contains(scenario.OrderId));
                    Assert.Equal(scenario.Open, verdicts.Browsable[scenario.OrderId]);
                    Assert.Equal(scenario.Open, verdicts.Take[scenario.OrderId] is null);
                }

                // The refusal is the one a missing order already returns. A key that named the hold —
                // or a second error beside this one — would let a cleaner infer from a rejection that
                // someone else was asked for by name, which is the whole property D5.2 buys.
                Assert.Equal(BusinessErrorMessage.OrderNotFound, verdicts.Take["pref-held-rival"]);

                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// The expiry needs no actor: the SAME rows, the same process, no write in between — only a later
    /// clock — and the withheld order is open on every surface. Anything that made expiry an event
    /// (a sweep, a status flip, a flag) would leave this red.
    /// </summary>
    [Fact]
    public async Task The_Hold_Expires_By_CLOCK_With_No_Code_Executed_In_Between()
    {
        await TestMethod(
            setup: ReplaceWithCallerSession,
            arrange: SeedTheHoldMatrix,
            act: async provider =>
            {
                var orderRepository = provider.GetRequiredService<IOrderRepository>();
                var afterExpiry = Future.AddMinutes(1);

                var duringHold = await OpenIdsAsync(orderRepository, DateTime.UtcNow);
                var afterHold = await OpenIdsAsync(orderRepository, afterExpiry);

                return (During: duringHold, After: afterHold);
            },
            assert: (CleansiaDbContext _, (List<string> During, List<string> After) result) =>
            {
                Assert.DoesNotContain("pref-held-rival", result.During);
                Assert.Contains("pref-held-rival", result.After);
                Assert.Equal(Cases.Length, result.After.Count);

                return Task.CompletedTask;
            });
    }

    private static Task<List<string>> OpenIdsAsync(IOrderRepository orderRepository, DateTime nowUtc) =>
        orderRepository
            .GetQueryable()
            .Where(OrderVisibility.NotHeldFrom(CallerEmployeeId, nowUtc))
            .Select(o => o.Id)
            .ToListAsync();

    private static GetPagedOrders.Request AvailableRequest() =>
        new()
        {
            Filter = EmptyFilter() with
            {
                HasAvailableSpots = true,
                ExcludeEmployeeId = CallerEmployeeId,
                OrderStatuses = OrderAvailability.OfferableStatuses.ToArray(),
            },
        };

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

    private static async Task SeedTheHoldMatrix(CleansiaDbContext context)
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
        var third = NewCleaner(
            ThirdEmployeeId, "user-pref-third", "cleaner-pref-third@cleansia.test", "Tomas", "Third");
        context.Add(caller);
        context.Add(NewCleaner(
            RivalEmployeeId, "user-pref-rival", "cleaner-pref-rival@cleansia.test", "Rita", "Rival"));
        context.Add(third);

        // Spread far enough apart that the take gate's time-conflict rule is never what refuses a row.
        var slot = 0;
        foreach (var scenario in Cases)
        {
            var order = NewOrder(scenario, DateTime.UtcNow.AddDays(2).AddHours(slot * 8));
            if (scenario.AssignedTo is not null)
            {
                order.SetMaxEmployees(2);
                order.AddAssignedEmployee(OrderEmployee.Create(order, third));
            }

            context.Add(order);
            slot++;
        }

        await context.CommitAsync(CancellationToken.None);
    }

    private static Order NewOrder(HoldCase scenario, DateTime cleaningDateTime)
    {
        var order = Order.Create(
            customerName: "Hold Customer",
            customerEmail: "hold-customer@cleansia.test",
            customerPhone: "+420777111444",
            customerAddress: Address.Create(
                "Held St 7", "Brno", "60200", CountryId, latitude: 49.19506, longitude: 16.606837),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            preferredEmployeeId: scenario.Beneficiary);
        order.Id = scenario.OrderId;
        order.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));

        if (scenario.HoldUntilUtc is not { } until)
        {
            return order;
        }

        if (scenario.Beneficiary is null)
        {
            // The stranded pair, reached the only way production could reach it: a write that bypassed
            // GrantPreferredHold, which refuses a deadline with no beneficiary. Nothing may WRITE this
            // row; the rule must still read it as open, because no actor is permitted to clear one.
            typeof(Order)
                .GetProperty(nameof(Order.PreferredHoldUntilUtc))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(order, [until]);
        }
        else
        {
            // Granted an hour before its own deadline, which is what production does — so an
            // already-expired fixture is a real past grant rather than a write the aggregate refuses.
            order.GrantPreferredHold(
                scenario.Beneficiary, until, until.AddHours(-1), BookingPolicy.MaxPreferredOfferRounds);
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
        employee.Approve(approvedByUserId: "admin-pref");
        employee.AssignWorkCountry(CountryId);
        employee.UpdateAddress(Address.Create("Cleaner St 9", "Brno", "60200", CountryId));
        employee.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        return employee;
    }

    public sealed record HoldCase(
        string OrderId,
        DateTime? HoldUntilUtc,
        string? Beneficiary,
        string? AssignedTo,
        bool Open);

    public sealed record Verdicts(
        PagedData<OrderListItem> Board,
        List<string> Counted,
        Dictionary<string, bool> Browsable,
        Dictionary<string, string?> Take);
}

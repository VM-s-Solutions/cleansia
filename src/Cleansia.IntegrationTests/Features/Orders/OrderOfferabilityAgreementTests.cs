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
/// TC-AVAIL-EQUIV + the ADR-0037 agreement pin, over REAL Postgres.
///
/// <para>T-0530 exists because five surfaces answered "which orders may a cleaner work on" with five
/// different status sets and nothing ever compared them. So the assertion that carries the weight is
/// not any single expected list — it is that the surfaces AGREE, per order, on one fixture, in one
/// run: what the paged board's server floor returns, what the dashboard count counts, and what the
/// take gate accepts.</para>
///
/// <para>And because the rule ships in two evaluation forms — a queryable one for the read surfaces
/// and an in-memory one for the write gate — a second test pins those against each other on the same
/// rows. Sharing one lambda would not have made them agree: SQL and C# disagree on null equality, so
/// the pairing has to be proven against the real provider rather than assumed.</para>
/// </summary>
[Collection("PostgresCollection")]
public class OrderOfferabilityAgreementTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-offer";
    private const string CountryId = "country-cz-offer";
    private const string EmployeeId = "employee-offer-1";
    private const string UserId = "user-offer-1";
    private const string EmployeeEmail = "cleaner-offer@cleansia.test";
    private const string RecurringTemplateId = "tpl-offer-weekly";

    /// <summary>
    /// The six rows ADR-0037 D1 rules on, plus the two Fact-3 rows the old status-blind seat
    /// arithmetic let through. Every one of them has a FREE SEAT and no assignee, so the seat term
    /// admits all eight and the verdict is decided purely by the rule under test.
    /// </summary>
    private static readonly OfferabilityCase[] Cases =
    [
        new("order-new-cash-oneoff", OrderStatus.New, PaymentType.Cash, PaymentStatus.Pending, null, true),
        new("order-new-card", OrderStatus.New, PaymentType.Card, PaymentStatus.Pending, null, false),
        new("order-new-cash-recur", OrderStatus.New, PaymentType.Cash, PaymentStatus.Pending, RecurringTemplateId, false),
        new("order-conf-cash-recur", OrderStatus.Confirmed, PaymentType.Cash, PaymentStatus.Paid, RecurringTemplateId, true),
        new("order-conf-card-paid", OrderStatus.Confirmed, PaymentType.Card, PaymentStatus.Paid, null, true),
        new("order-conf-card-pend", OrderStatus.Confirmed, PaymentType.Card, PaymentStatus.Pending, null, false),
        new("order-cancelled-seat", OrderStatus.Cancelled, PaymentType.Cash, PaymentStatus.Pending, null, false),
        new("order-completed-seat", OrderStatus.Completed, PaymentType.Card, PaymentStatus.Paid, null, false),
    ];

    [Fact]
    public async Task The_Board_The_Dashboard_Count_And_The_Take_Gate_Agree_Order_By_Order()
    {
        await TestMethod(
            setup: services => ReplaceWithEmployeeSession(services),
            arrange: SeedTheOfferabilityMatrix,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var board = await mediator.Send(AvailableRequest());

                var orderRepository = provider.GetRequiredService<IOrderRepository>();
                var counted = await orderRepository
                    .GetQueryable()
                    .Where(DashboardSpecifications.CreateAvailableOrdersSpec(EmployeeId, DateTime.UtcNow).SatisfiedBy())
                    .Select(o => o.Id)
                    .ToListAsync();

                var validator = new TakeOrder.Validator(
                    orderRepository,
                    provider.GetRequiredService<IEmployeeRepository>(),
                    provider.GetRequiredService<IOrderAccessService>());

                var takeVerdicts = new Dictionary<string, string?>();
                foreach (var scenario in Cases)
                {
                    var result = await validator.ValidateAsync(new TakeOrder.Command(scenario.OrderId));
                    takeVerdicts[scenario.OrderId] = result.IsValid
                        ? null
                        : Assert.Single(result.Errors).ErrorMessage;
                }

                return new Verdicts(board, counted, takeVerdicts);
            },
            assert: (CleansiaDbContext _, Verdicts verdicts) =>
            {
                var listed = verdicts.Board.Data!.Select(o => o.Id).ToHashSet();

                foreach (var scenario in Cases)
                {
                    var takeable = verdicts.Take[scenario.OrderId] is null;

                    Assert.Equal(scenario.Offerable, listed.Contains(scenario.OrderId));
                    Assert.Equal(scenario.Offerable, verdicts.Counted.Contains(scenario.OrderId));
                    Assert.Equal(scenario.Offerable, takeable);
                }

                // The two terminal states reuse the keys the same family already emits; everything
                // else the money axis refuses gets the one opaque residue key.
                Assert.Equal(BusinessErrorMessage.OrderAlreadyCancelled, verdicts.Take["order-cancelled-seat"]);
                Assert.Equal(BusinessErrorMessage.OrderAlreadyCompleted, verdicts.Take["order-completed-seat"]);
                Assert.Equal(BusinessErrorMessage.OrderNotTakeable, verdicts.Take["order-new-card"]);
                Assert.Equal(BusinessErrorMessage.OrderNotTakeable, verdicts.Take["order-new-cash-recur"]);
                Assert.Equal(BusinessErrorMessage.OrderNotTakeable, verdicts.Take["order-conf-card-pend"]);

                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// The client sends the status set it always sent. After the server floor carries the rule, a
    /// non-offerable row is not returned no matter what the client asks for — which is what demotes
    /// the three client status lists from an authorization boundary to a display refinement.
    /// </summary>
    [Fact]
    public async Task A_Client_Asking_For_Every_Status_Still_Gets_Only_Offerable_Rows()
    {
        await TestMethod(
            setup: services => ReplaceWithEmployeeSession(services),
            arrange: SeedTheOfferabilityMatrix,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(new GetPagedOrders.Request
                {
                    Filter = EmptyFilter() with { OrderStatuses = Enum.GetValues<OrderStatus>() },
                });
            },
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                var listed = page.Data!.Select(o => o.Id).ToHashSet();

                foreach (var scenario in Cases)
                {
                    Assert.Equal(scenario.Offerable, listed.Contains(scenario.OrderId));
                }

                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task The_Queryable_Form_And_The_In_Memory_Form_Select_The_Same_Rows()
    {
        await TestMethod(
            setup: _ => Task.CompletedTask,
            arrange: SeedTheOfferabilityMatrix,
            act: async provider =>
            {
                var repository = provider.GetRequiredService<IOrderRepository>();

                var fromSql = await repository
                    .GetQueryable()
                    .Where(OrderAvailability.IsOfferableSql)
                    .Select(o => o.Id)
                    .ToListAsync();

                var rows = await repository
                    .GetQueryable()
                    .Select(o => new
                    {
                        o.Id,
                        o.CurrentStatus,
                        o.PaymentType,
                        o.PaymentStatus,
                        o.RecurringTemplateId,
                    })
                    .ToListAsync();

                var fromMemory = rows
                    .Where(o => OrderAvailability.IsOfferable(
                        o.CurrentStatus, o.PaymentType, o.PaymentStatus, o.RecurringTemplateId))
                    .Select(o => o.Id)
                    .ToList();

                return (Sql: fromSql, Memory: fromMemory, RowCount: rows.Count);
            },
            assert: (CleansiaDbContext _, (List<string> Sql, List<string> Memory, int RowCount) result) =>
            {
                Assert.Equal(Cases.Length, result.RowCount);
                Assert.Equal(result.Sql.OrderBy(id => id), result.Memory.OrderBy(id => id));
                Assert.Equal(
                    Cases.Where(c => c.Offerable).Select(c => c.OrderId).OrderBy(id => id),
                    result.Sql.OrderBy(id => id));

                return Task.CompletedTask;
            });
    }

    private static GetPagedOrders.Request AvailableRequest() =>
        new()
        {
            Filter = EmptyFilter() with
            {
                HasAvailableSpots = true,
                ExcludeEmployeeId = EmployeeId,
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

    private static Task ReplaceWithEmployeeSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            UserId,
            EmployeeEmail,
            [
                new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()),
                new Claim(TestUserSessionProvider.EmployeeIdClaimType, EmployeeId),
            ])));
        return Task.CompletedTask;
    }

    private static async Task SeedTheOfferabilityMatrix(CleansiaDbContext context)
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
            EmployeeEmail, TestUtilities.Constants.TestUserSession.TestUserPassword, "Olga", "Offer", UserProfile.Employee);
        user.Id = UserId;
        user.ConfirmEmail();
        user.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        var employee = Employee.CreateWithUser(user);
        employee.Id = EmployeeId;
        employee.Approve(approvedByUserId: "admin-offer");
        employee.AssignWorkCountry(CountryId);
        employee.UpdateAddress(Address.Create("Cleaner St 3", "Brno", "60200", CountryId));
        employee.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        context.Add(employee);

        // Spread the cleaning times so no two fixtures overlap on the cleaner's calendar — the take
        // gate's time-conflict rule must not be what refuses a row this test is asking about.
        var slot = 0;
        foreach (var scenario in Cases)
        {
            context.Add(CreateOrder(scenario, DateTime.UtcNow.AddDays(2).AddHours(slot * 8)));
            slot++;
        }

        await context.CommitAsync(CancellationToken.None);
    }

    private static Order CreateOrder(OfferabilityCase scenario, DateTime cleaningDateTime)
    {
        var order = Order.Create(
            customerName: "Offer Customer",
            customerEmail: "offer-customer@cleansia.test",
            customerPhone: "+420777111222",
            customerAddress: Address.Create("Open St 1", "Brno", "60200", CountryId, latitude: 49.19506, longitude: 16.606837),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: cleaningDateTime,
            paymentType: scenario.PaymentType,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: scenario.PaymentStatus,
            recurringTemplateId: scenario.RecurringTemplateId);
        order.Id = scenario.OrderId;
        order.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        if (scenario.Status != OrderStatus.New)
        {
            order.AddOrderStatus(OrderStatusTrack.Create(scenario.Status, order));
        }

        return order;
    }

    public sealed record OfferabilityCase(
        string OrderId,
        OrderStatus Status,
        PaymentType PaymentType,
        PaymentStatus PaymentStatus,
        string? RecurringTemplateId,
        bool Offerable);

    public sealed record Verdicts(
        PagedData<OrderListItem> Board,
        List<string> Counted,
        Dictionary<string, string?> Take);
}

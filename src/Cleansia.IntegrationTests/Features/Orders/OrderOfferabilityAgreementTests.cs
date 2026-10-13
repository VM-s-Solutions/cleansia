using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Dashboard;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
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
using Cleansia.Infra.Common.Validations;
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
    private static readonly DateTime NowUtc = DateTime.UtcNow;
    private const string RecurringTemplateId = "tpl-offer-weekly";

    /// <summary>
    /// Status/payment cases have an unassigned free seat. Temporal cases include a late fill,
    /// two long started jobs with another cleaner and a free seat, and exact/after-end refusals.
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
        // Started, but not over: the crew is short, the seat is open, the job has not happened yet.
        new("order-otw-card-paid", OrderStatus.OnTheWay, PaymentType.Card, PaymentStatus.Paid, null, true),
        new("order-inprog-card-paid", OrderStatus.InProgress, PaymentType.Card, PaymentStatus.Paid, null, true),
        // The money axis must not be swallowed by the widened status axis.
        new("order-otw-card-pend", OrderStatus.OnTheWay, PaymentType.Card, PaymentStatus.Pending, null, false),
        new("order-inprog-cash-recur", OrderStatus.InProgress, PaymentType.Cash, PaymentStatus.Pending, RecurringTemplateId, false),
        // Owner ruling 2026-09-28: the customer's confirmation admits a recurring CASH occurrence while it
        // is still unpaid, and only cash — a card occurrence is confirmed by its payment.
        new("order-new-cash-recur-conf", OrderStatus.New, PaymentType.Cash, PaymentStatus.Pending, RecurringTemplateId, true, CustomerConfirmed: true),
        new("order-new-card-recur-conf", OrderStatus.New, PaymentType.Card, PaymentStatus.Pending, RecurringTemplateId, false, CustomerConfirmed: true),
        new("order-late-fill", OrderStatus.New, PaymentType.Cash, PaymentStatus.Pending, null, true, StartOffsetMinutes: -10, DurationMinutes: 60),
        new("order-started-180", OrderStatus.OnTheWay, PaymentType.Card, PaymentStatus.Paid, null, true, StartOffsetMinutes: -130, DurationMinutes: 180, AssignedOtherCleaner: true),
        new("order-started-240", OrderStatus.InProgress, PaymentType.Card, PaymentStatus.Paid, null, true, StartOffsetMinutes: -130, DurationMinutes: 240, AssignedOtherCleaner: true),
        new("order-slot-end", OrderStatus.New, PaymentType.Cash, PaymentStatus.Pending, null, false, StartOffsetMinutes: -60, DurationMinutes: 60),
        new("order-started-end", OrderStatus.InProgress, PaymentType.Card, PaymentStatus.Paid, null, false, StartOffsetMinutes: -240, DurationMinutes: 240),
        new("order-after-end", OrderStatus.OnTheWay, PaymentType.Card, PaymentStatus.Paid, null, false, StartOffsetMinutes: -181, DurationMinutes: 180),
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
                var currencyResolution = provider.GetRequiredService<ICurrencyResolutionService>();
                var currency = await currencyResolution.ResolveCurrencyForEmployeeAsync(EmployeeId, CancellationToken.None);
                var counted = await orderRepository
                    .GetQueryable()
                    .Where(DashboardSpecifications.CreateAvailableOrdersSpec(EmployeeId, currency.Id, DateTime.UtcNow).SatisfiedBy())
                    .Select(o => o.Id)
                    .ToListAsync();

                var validator = new TakeOrder.Validator(
                    orderRepository,
                    provider.GetRequiredService<IEmployeeRepository>(),
                    provider.GetRequiredService<IOrderAccessService>(),
                    currencyResolution,
                    provider.GetRequiredService<ILegalDocumentRepository>(),
                    provider.GetRequiredService<ILegalDocumentResolver>(),
                    provider.GetRequiredService<IUserConsentRepository>());

                var takeVerdicts = new Dictionary<string, string?>();
                foreach (var scenario in Cases)
                {
                    var result = await validator.ValidateAsync(new TakeOrder.Command(scenario.OrderId, TestLegalDocuments.WorkContractTextEnId));
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

                // The two terminal states carry the take gate's own persona-specific keys; everything
                // else the money axis refuses gets the one opaque residue key.
                Assert.Equal(BusinessErrorMessage.TakeOrderAlreadyCancelled, verdicts.Take["order-cancelled-seat"]);
                Assert.Equal(BusinessErrorMessage.TakeOrderAlreadyCompleted, verdicts.Take["order-completed-seat"]);
                Assert.Equal(BusinessErrorMessage.OrderNotTakeable, verdicts.Take["order-new-card"]);
                Assert.Equal(BusinessErrorMessage.OrderNotTakeable, verdicts.Take["order-new-cash-recur"]);
                Assert.Equal(BusinessErrorMessage.OrderNotTakeable, verdicts.Take["order-conf-card-pend"]);

                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task Slot_End_Agrees_On_Current_And_Seat_Boards_Browse_Contract_Preview_And_Dashboard()
    {
        await TestMethod(
            setup: ReplaceWithEmployeeSession,
            arrange: SeedTheOfferabilityMatrix,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var board = await mediator.Send(AvailableRequest());
                var native = await mediator.Send(new GetPagedOrders.Request
                {
                    Filter = EmptyFilter() with { IsUnassigned = true, OrderStatuses = OrderAvailability.OfferableStatuses.ToArray() },
                    Limit = 50,
                });
                var preview = await mediator.Send(new GetAvailableJobsPreview.Query(50));
                Assert.True(preview.IsSuccess, preview.Error?.Message);
                var repository = provider.GetRequiredService<IOrderRepository>();
                var access = provider.GetRequiredService<IOrderAccessService>();
                var currency = provider.GetRequiredService<ICurrencyResolutionService>();
                var counted = await repository.GetQueryable()
                    .Where(DashboardSpecifications.CreateAvailableOrdersSpec(EmployeeId, CurrencyId, DateTime.UtcNow).SatisfiedBy())
                    .Select(o => o.Id).ToListAsync();
                var contractValidator = new GetWorkContractPreview.Validator(repository, access, currency);
                var browse = new Dictionary<string, bool>();
                var contract = new Dictionary<string, bool>();
                foreach (var scenario in Cases.Where(c => c.StartOffsetMinutes.HasValue))
                {
                    var order = await repository.GetQueryable().SingleAsync(o => o.Id == scenario.OrderId);
                    browse[order.Id] = await access.CanBrowseOrderAsync(order, CancellationToken.None);
                    var validation = await contractValidator.ValidateAsync(new GetWorkContractPreview.Query(order.Id));
                    contract[order.Id] = validation.IsValid;
                    if (!scenario.Offerable)
                    {
                        var detail = await mediator.Send(new GetOrderDetails.Query(order.Id));
                        Assert.True(detail.IsFailure);
                        Assert.Equal(BusinessErrorMessage.OrderNotFound, detail.Error?.Message);
                        var take = await mediator.Send(new TakeOrder.Command(order.Id, TestLegalDocuments.WorkContractTextEnId));
                        Assert.True(take.IsFailure);
                        Assert.Equal(BusinessErrorMessage.OrderNotTakeable, Assert.Single(Assert.IsAssignableFrom<IValidationResult>(take).Errors).Message);
                        Assert.Empty(order.AssignedEmployees);
                    }
                }
                return new SlotSurfaceVerdicts(board.Data!.Select(o => o.Id).ToHashSet(), native.Data!.Select(o => o.Id).ToHashSet(),
                    counted.ToHashSet(), preview.Value.Jobs.Select(o => o.Id).ToHashSet(), browse, contract);
            },
            assert: (CleansiaDbContext _, SlotSurfaceVerdicts verdicts) =>
            {
                foreach (var scenario in Cases.Where(c => c.StartOffsetMinutes.HasValue))
                {
                    Assert.Equal(scenario.Offerable, verdicts.Board.Contains(scenario.OrderId));
                    Assert.Equal(scenario.Offerable && !scenario.AssignedOtherCleaner, verdicts.Native.Contains(scenario.OrderId));
                    Assert.Equal(scenario.Offerable, verdicts.Counted.Contains(scenario.OrderId));
                    Assert.Equal(scenario.Offerable, verdicts.Preview.Contains(scenario.OrderId));
                    Assert.Equal(scenario.Offerable, verdicts.Browse[scenario.OrderId]);
                    Assert.Equal(scenario.Offerable, verdicts.Contract[scenario.OrderId]);
                }
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task An_Ended_Assignment_Remains_In_Own_Reads_And_Its_Contract_Preview()
    {
        await TestMethod(
            setup: ReplaceWithEmployeeSession,
            arrange: async context =>
            {
                await SeedTheOfferabilityMatrix(context);
                var employee = (await context.Employees.FindAsync(EmployeeId))!;
                var order = (await context.Orders.FindAsync("order-started-end"))!;
                order.AddAssignedEmployee(OrderEmployee.Create(order, employee));
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var mine = await mediator.Send(new GetPagedOrders.Request { Filter = EmptyFilter() with { EmployeeId = EmployeeId } });
                Assert.Contains(mine.Data!, row => row.Id == "order-started-end");
                var detail = await mediator.Send(new GetOrderDetails.Query("order-started-end"));
                Assert.True(detail.IsSuccess, detail.Error?.Message);
                var validation = await new GetWorkContractPreview.Validator(
                    provider.GetRequiredService<IOrderRepository>(), provider.GetRequiredService<IOrderAccessService>(),
                    provider.GetRequiredService<ICurrencyResolutionService>())
                    .ValidateAsync(new GetWorkContractPreview.Query("order-started-end"));
                Assert.True(validation.IsValid);
                var ended = await provider.GetRequiredService<IOrderRepository>().GetQueryable().SingleAsync(o => o.Id == "order-started-end");
                var owned = Order.Create("Owner", "owner@cleansia.test", "+420777111222", ended.CustomerAddress!,
                    2, 1, ended.CleaningDateTime, PaymentType.Card, 1500m, CurrencyId, PaymentStatus.Paid,
                    userId: UserId, cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
                owned.UpdateEstimatedTime(60);
                owned.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.InProgress, owned));
                foreach (var role in new[] { UserProfile.Customer, UserProfile.Administrator })
                {
                    var access = new OrderAccessService(new TestUserSessionProvider(UserId, EmployeeEmail, [new Claim(ClaimTypes.Role, role.ToString())]),
                        provider.GetRequiredService<IEmployeeRepository>(), provider.GetRequiredService<IOrderRepository>(),
                        provider.GetRequiredService<ICurrencyResolutionService>(), provider.GetRequiredService<ICashLedgerRepository>(),
                        provider.GetRequiredService<IAppConfigurationProvider>());
                    Assert.True(await access.CanBrowseOrderAsync(owned, CancellationToken.None));
                }
                return true;
            },
            assert: (CleansiaDbContext _, bool passed) => { Assert.True(passed); return Task.CompletedTask; });
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
                    .Where(OrderAvailability.IsOfferableSql(NowUtc))
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
                        o.CustomerConfirmedAt,
                        o.CleaningDateTime,
                        o.EstimatedTime,
                    })
                    .ToListAsync();

                var fromMemory = rows
                    .Where(o => OrderAvailability.IsOfferable(
                        o.CurrentStatus, o.PaymentType, o.PaymentStatus, o.RecurringTemplateId, o.CustomerConfirmedAt, o.CleaningDateTime, o.EstimatedTime, NowUtc))
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
        TestLegalDocuments.Add(context);

        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        context.CountryConfigurations.Add(CountryConfiguration.Create(CountryId, "CZK", "cs", 0.21m));

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.IsActive = true;
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

        var colleagueUser = User.CreateWithPassword("colleague@cleansia.test", TestUtilities.Constants.TestUserSession.TestUserPassword,
            "Other", "Cleaner", UserProfile.Employee);
        colleagueUser.Id = "user-offer-other";
        colleagueUser.ConfirmEmail();
        colleagueUser.Created(TestUtilities.Constants.TestUserSession.TestUserName, NowUtc);
        var colleague = Employee.CreateWithUser(colleagueUser);
        colleague.Id = "employee-offer-other";
        colleague.Approve("admin-offer");
        colleague.AssignWorkCountry(CountryId);
        colleague.Created(TestUtilities.Constants.TestUserSession.TestUserName, NowUtc);
        context.Add(colleague);

        // Spread the cleaning times so no two fixtures overlap on the cleaner's calendar — the take
        // gate's time-conflict rule must not be what refuses a row this test is asking about.
        var slot = 0;
        foreach (var scenario in Cases)
        {
            var order = CreateOrder(scenario, scenario.StartOffsetMinutes is { } offset
                ? NowUtc.AddMinutes(offset)
                : NowUtc.AddDays(2).AddHours(slot * 8));
            if (scenario.AssignedOtherCleaner)
            {
                order.AddAssignedEmployee(OrderEmployee.Create(order, colleague));
            }
            context.Add(order);
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
            cleaningDateTime: cleaningDateTime,
            paymentType: scenario.PaymentType,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: scenario.PaymentStatus,
            recurringTemplateId: scenario.RecurringTemplateId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.UpdateEstimatedTime(scenario.DurationMinutes).CalculateRequiredEmployees(BookingPolicy.SpareSeatsPerOrder);
        order.Id = scenario.OrderId;
        order.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        order.SetWorkContractDocument(WorkContractTestData.Document());

        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        if (scenario.CustomerConfirmed)
        {
            order.ConfirmByCustomer(DateTime.UtcNow);
        }

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
        bool Offerable,
        bool CustomerConfirmed = false,
        int? StartOffsetMinutes = null,
        int DurationMinutes = 60,
        bool AssignedOtherCleaner = false);

    private sealed record SlotSurfaceVerdicts(
        HashSet<string> Board, HashSet<string> Native, HashSet<string> Counted, HashSet<string> Preview,
        Dictionary<string, bool> Browse, Dictionary<string, bool> Contract);

    public sealed record Verdicts(
        PagedData<OrderListItem> Board,
        List<string> Counted,
        Dictionary<string, string?> Take);
}

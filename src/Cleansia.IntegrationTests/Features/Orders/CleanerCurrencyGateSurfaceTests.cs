using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Dashboard;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.Filters;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
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
/// A cleaner is paid in the currency of the country they work in (owner ruling 2026-09-12): a CZ cleaner
/// in CZK, so an order priced in EUR is not on their board, not in their count, not openable and not
/// takeable — while the order they are already on stays theirs whatever it is priced in (the admin
/// override). Over real Postgres because the rule ships in two forms — a queryable one for the board
/// and the take gate, a materialized one for the browse gate — and the two are pinned to each other
/// here rather than by review.
/// </summary>
[Collection("PostgresCollection")]
public class CleanerCurrencyGateSurfaceTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CzkId = "currency-czk-ccgate";
    private const string EurId = "currency-eur-ccgate";
    private const string CountryId = "country-cz-ccgate";
    private const string CallerEmployeeId = "employee-ccgate-caller";
    private const string CallerUserId = "user-ccgate-caller";
    private const string CallerEmail = "cleaner-ccgate@cleansia.test";

    private const string CzkOrderId = "order-ccgate-czk";
    private const string EurOrderId = "order-ccgate-eur";
    private const string EurAssignedOrderId = "order-ccgate-eur-mine";

    private static readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public async Task The_Board_The_Count_The_Browse_Gate_And_The_Take_Gate_Agree_On_The_Currency()
    {
        await TestMethod(
            setup: ReplaceWithCallerSession,
            arrange: SeedAsync,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var board = await mediator.Send(AvailableRequest());
                var mine = await mediator.Send(MineRequest());
                var preview = await mediator.Send(new GetAvailableJobsPreview.Query(Limit: 5));

                var orderRepository = provider.GetRequiredService<IOrderRepository>();
                var accessService = provider.GetRequiredService<IOrderAccessService>();
                var currencyResolution = provider.GetRequiredService<ICurrencyResolutionService>();
                var currency = await currencyResolution.ResolveCurrencyForEmployeeAsync(CallerEmployeeId, CancellationToken.None);

                var counted = await orderRepository
                    .GetQueryable()
                    .Where(DashboardSpecifications.CreateAvailableOrdersSpec(CallerEmployeeId, currency.Id, Now).SatisfiedBy())
                    .Select(o => o.Id)
                    .ToListAsync();

                var fromSql = await orderRepository
                    .GetQueryable()
                    .Where(OrderVisibility.OpenTo(CallerEmployeeId, currency.Id, Now))
                    .Select(o => o.Id)
                    .ToListAsync();
                var fromMemory = (await orderRepository.GetQueryable().Include(o => o.AssignedEmployees).ToListAsync())
                    .Where(o => OrderVisibility.OpenTo(o, CallerEmployeeId, currency.Id, Now))
                    .Select(o => o.Id)
                    .ToList();

                var validator = new TakeOrder.Validator(
                    orderRepository,
                    provider.GetRequiredService<IEmployeeRepository>(),
                    accessService,
                    currencyResolution);

                var browsable = new Dictionary<string, bool>();
                var takeVerdicts = new Dictionary<string, string?>();
                foreach (var orderId in new[] { CzkOrderId, EurOrderId, EurAssignedOrderId })
                {
                    var order = await orderRepository.GetByIdAsync(orderId, CancellationToken.None);
                    browsable[orderId] = await accessService.CanBrowseOrderAsync(order!, CancellationToken.None);

                    var result = await validator.ValidateAsync(new TakeOrder.Command(orderId));
                    takeVerdicts[orderId] = result.IsValid ? null : Assert.Single(result.Errors).ErrorMessage;
                }

                return new Verdicts(
                    currency.Code,
                    board.Data!.Select(o => o.Id).ToHashSet(),
                    mine.Data!.Select(o => o.Id).ToHashSet(),
                    preview.Value!.Jobs.Select(j => j.Id).ToHashSet(),
                    preview.Value.TotalAvailableCount,
                    counted.ToHashSet(),
                    fromSql,
                    fromMemory,
                    browsable,
                    takeVerdicts);
            },
            assert: (CleansiaDbContext _, Verdicts verdicts) =>
            {
                Assert.Equal("CZK", verdicts.ResolvedCode);

                // The board, the preview and the dashboard count: CZK yes, EUR no.
                Assert.Contains(CzkOrderId, verdicts.Board);
                Assert.DoesNotContain(EurOrderId, verdicts.Board);
                Assert.Contains(CzkOrderId, verdicts.Preview);
                Assert.DoesNotContain(EurOrderId, verdicts.Preview);
                Assert.Equal(1, verdicts.PreviewCount);
                Assert.Equal(new[] { CzkOrderId }, verdicts.Counted);

                // The admin override: the EUR order the cleaner is already on is still in "mine".
                Assert.Contains(EurAssignedOrderId, verdicts.Mine);

                // The browse gate and the take gate read the same rule as the board.
                Assert.True(verdicts.Browsable[CzkOrderId]);
                Assert.False(verdicts.Browsable[EurOrderId]);
                Assert.True(verdicts.Browsable[EurAssignedOrderId]);
                Assert.Null(verdicts.Take[CzkOrderId]);
                Assert.Equal(BusinessErrorMessage.OrderNotFound, verdicts.Take[EurOrderId]);
                Assert.Equal(BusinessErrorMessage.EmployeeAlreadyAssignedToOrder, verdicts.Take[EurAssignedOrderId]);

                // The two evaluation forms select the same rows over real Postgres.
                Assert.Equal(verdicts.FromSql.OrderBy(id => id), verdicts.FromMemory.OrderBy(id => id));
                Assert.Equal(
                    new[] { CzkOrderId, EurAssignedOrderId }.OrderBy(id => id),
                    verdicts.FromSql.OrderBy(id => id));

                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// The refusal is a business result, not an absent row: driven through the real command so the
    /// pipeline's validator is what answers.
    /// </summary>
    [Fact]
    public async Task Taking_An_Order_In_Another_Currency_Is_Refused_Through_The_Command()
    {
        await TestMethod(
            setup: ReplaceWithCallerSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new TakeOrder.Command(EurOrderId)),
            assert: async (CleansiaDbContext context, BusinessResult<TakeOrder.Response> result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Equal(BusinessErrorMessage.OrderNotFound, Assert.Single(validation.Errors).Message);

                var assigned = await context.Set<OrderEmployee>()
                    .IgnoreQueryFilters()
                    .AnyAsync(oe => oe.OrderId == EurOrderId);
                Assert.False(assigned);
            });
    }

    private static GetPagedOrders.Request AvailableRequest() =>
        new()
        {
            Filter = EmptyFilter() with
            {
                IsUnassigned = true,
                HasAvailableSpots = true,
                OrderStatuses = new[] { OrderStatus.New, OrderStatus.Confirmed },
            },
        };

    private static GetPagedOrders.Request MineRequest() =>
        new()
        {
            Filter = EmptyFilter() with
            {
                EmployeeId = CallerEmployeeId,
                OrderStatuses = new[] { OrderStatus.Confirmed, OrderStatus.InProgress, OrderStatus.Completed },
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

    private static async Task SeedAsync(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        // CZ pays in CZK by configuration, not by falling through to the platform default.
        context.CountryConfigurations.Add(CountryConfiguration.Create(CountryId, "CZK", "cs", 0.21m));

        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.IsActive = true;
        czk.Id = CzkId;
        czk.SetAsDefault(true);
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.IsActive = true;
        eur.Id = EurId;
        context.Currencies.AddRange(czk, eur);

        var cleaner = NewCleaner();
        context.Add(cleaner);

        context.Add(NewOfferableOrder(CzkOrderId, CzkId, Now.AddDays(2)));
        context.Add(NewOfferableOrder(EurOrderId, EurId, Now.AddDays(2).AddHours(4)));

        // Two seats, one taken by the caller, on a day of its own so it is not a time conflict for the
        // others: the take gate then answers on the assignment, not on the seat or the clock.
        var assignedByAdmin = NewOfferableOrder(EurAssignedOrderId, EurId, Now.AddDays(3));
        assignedByAdmin.SetMaxEmployees(2);
        assignedByAdmin.AddAssignedEmployee(OrderEmployee.Create(assignedByAdmin, cleaner));
        context.Add(assignedByAdmin);

        await context.CommitAsync(CancellationToken.None);
    }

    private static Order NewOfferableOrder(string id, string currencyId, DateTime cleaningDateTime)
    {
        var order = Order.Create(
            customerName: "Gate Customer",
            customerEmail: "gate-customer@cleansia.test",
            customerPhone: "+420777111222",
            // Real coordinates, so the list's background geocode never fires inside the test transaction.
            customerAddress: Address.Create("Gate St 1", "Praha", "12000", CountryId, latitude: 50.0755, longitude: 14.4378),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: currencyId,
            paymentStatus: PaymentStatus.Paid);
        order.Id = id;
        order.UpdateEstimatedTime(120);
        order.SetMaxEmployees(1);
        order.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        return order;
    }

    private static Employee NewCleaner()
    {
        var user = User.CreateWithPassword(
            CallerEmail,
            TestUtilities.Constants.TestUserSession.TestUserPassword,
            "Petra",
            "Gate",
            UserProfile.Employee);
        user.Id = CallerUserId;
        user.ConfirmEmail();
        user.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        var employee = Employee.CreateWithUser(user);
        employee.Id = CallerEmployeeId;
        employee.Approve(approvedByUserId: "admin-ccgate");
        employee.AssignWorkCountry(CountryId);
        employee.UpdateAddress(Address.Create("Cleaner St 9", "Praha", "12000", CountryId));
        employee.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        return employee;
    }

    private sealed record Verdicts(
        string ResolvedCode,
        HashSet<string> Board,
        HashSet<string> Mine,
        HashSet<string> Preview,
        int PreviewCount,
        HashSet<string> Counted,
        List<string> FromSql,
        List<string> FromMemory,
        Dictionary<string, bool> Browsable,
        Dictionary<string, string?> Take);
}

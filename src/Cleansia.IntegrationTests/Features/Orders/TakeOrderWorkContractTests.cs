using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// ADR-0068 D3 (Verification #3) on real Postgres: a take with a text of the order's document writes the
/// seat, the status row, the acceptance (the new seat, the echoed text, the device CLAIM, the builder's
/// facts) and the audit row in ONE commit; a take without a text or with a text of another document
/// leaves nothing; the seat-race loser's whole commit rolls back so no acceptance exists for a seat
/// that was never won; the cover take leaves the displaced cleaner's row on their old seat; the
/// beneficiary of a held order takes it with the acceptance.
/// </summary>
[Collection("PostgresCollection")]
public sealed class TakeOrderWorkContractTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CzkId = "currency-czk-wctake";
    private const string CountryId = "country-cz-wctake";
    private const string CallerEmployeeId = "employee-wctake-caller";
    private const string CallerUserId = "user-wctake-caller";
    private const string CallerEmail = "cleaner-wctake@cleansia.test";
    private const string DeviceClaim = "device-claim-wctake-1";
    private const string OtherEmployeeId = "employee-wctake-other";
    private const string OtherUserId = "user-wctake-other";

    private const string OpenOrderId = "order-wctake-open";
    private const string CoverOrderId = "order-wctake-cover";
    private const string HeldOrderId = "order-wctake-held";
    private const string DisplacedSeatId = "01SEATWCTAKEDISPLACED00001";

    private static readonly DateTime Now = DateTime.UtcNow;

    private static Task CallerSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            CallerUserId,
            CallerEmail,
            [
                new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()),
                new Claim(TestUserSessionProvider.EmployeeIdClaimType, CallerEmployeeId),
                new Claim(AuthExtensions.DeviceIdClaimType, DeviceClaim),
            ])));
        return Task.CompletedTask;
    }

    private static async Task SeedAsync(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        var (document, _) = TestLegalDocuments.Add(context);

        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        context.CountryConfigurations.Add(CountryConfiguration.Create(CountryId, "CZK", "cs", 0.21m));

        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.IsActive = true;
        czk.Id = CzkId;
        czk.SetAsDefault(true);
        context.Currencies.Add(czk);

        var caller = NewCleaner(CallerUserId, CallerEmail, CallerEmployeeId);
        var other = NewCleaner(OtherUserId, "cleaner-wctake-other@cleansia.test", OtherEmployeeId);
        context.AddRange(caller, other);

        context.Add(NewOrder(OpenOrderId, Now.AddDays(2), document));

        // A seat whose holder asked for cover, with the contract they accepted on it.
        var covered = NewOrder(CoverOrderId, Now.AddDays(3), document);
        var displacedSeat = OrderEmployee.Create(covered, other);
        displacedSeat.Id = DisplacedSeatId;
        covered.AddAssignedEmployee(displacedSeat);
        displacedSeat.MarkCoverRequested(Now.AddHours(-1));
        context.Add(covered);
        context.Add(WorkContractAcceptance.Create(
            CoverOrderId, DisplacedSeatId, OtherEmployeeId, document.TextFor("en")!, document.Version,
            "cleansia.partner", "198.51.100.7", "Firefox", null, "{\"orderNumber\":\"ORD-COVER\"}"));

        var held = NewOrder(HeldOrderId, Now.AddDays(4), document);
        held.GrantPreferredHold(CallerEmployeeId, Now.AddHours(6), Now, 3);
        context.Add(held);

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_Take_With_A_Text_Of_The_Orders_Document_Writes_The_Seat_The_Acceptance_And_The_Audit_Row_Together()
    {
        await TestMethod(
            setup: CallerSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new TakeOrder.Command(OpenOrderId, TestLegalDocuments.WorkContractTextCsId)),
            assert: async (CleansiaDbContext context, BusinessResult<TakeOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);

                var order = await context.Orders.IgnoreQueryFilters()
                    .Include(o => o.AssignedEmployees)
                    .Include(o => o.OrderStatusHistory)
                    .AsSplitQuery()
                    .SingleAsync(o => o.Id == OpenOrderId);
                var seat = Assert.Single(order.AssignedEmployees);
                Assert.Equal(CallerEmployeeId, seat.EmployeeId);
                Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);

                var acceptance = Assert.Single(await context.WorkContractAcceptances.IgnoreQueryFilters()
                    .Where(a => a.OrderId == OpenOrderId).ToListAsync());
                Assert.Equal(seat.Id, acceptance.OrderEmployeeId);
                Assert.Equal(CallerEmployeeId, acceptance.EmployeeId);
                Assert.Equal(TestLegalDocuments.WorkContractTextCsId, acceptance.LegalDocumentTextId);
                Assert.Equal(WorkContractTestData.Version, acceptance.DocumentVersion);
                Assert.Equal(DeviceClaim, acceptance.DeviceId);
                Assert.Equal(TestTenants.Default, acceptance.TenantId);
                Assert.Equal(CallerUserId, acceptance.CreatedBy);
                Assert.InRange(acceptance.AcceptedOn, Now.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));
                Assert.Equal(JwtAudiences.Customer, acceptance.ClientAudience);

                var facts = WorkContractFacts.FromJson(acceptance.FactsJson);
                Assert.Equal(order.DisplayOrderNumber, facts.OrderNumber);
                Assert.Equal("Praha · 120 xx", facts.LocationApproximate);
                Assert.Equal("CZK", facts.CurrencyCode);
                Assert.Equal(1500m, facts.TotalPrice);
                Assert.Equal(120, facts.EstimatedMinutes);
                Assert.Equal(CountryId, facts.CountryId);

                var audit = Assert.Single(await context.EmployeeActionAudits.IgnoreQueryFilters()
                    .Where(a => a.OrderId == OpenOrderId).ToListAsync());
                Assert.Equal(EmployeeAuditAction.ContractAccepted, audit.Action);
                Assert.Equal(CallerEmployeeId, audit.EmployeeId);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Take_Without_A_Text_Is_Refused_As_Not_Accepted_And_Writes_Nothing()
    {
        await TestMethod(
            setup: CallerSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new TakeOrder.Command(OpenOrderId, null)),
            assert: async (CleansiaDbContext context, BusinessResult<TakeOrder.Response> result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Equal(BusinessErrorMessage.WorkContractNotAccepted, Assert.Single(validation.Errors).Message);
                await AssertNothingWrittenAsync(context, OpenOrderId);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Take_With_A_Text_Of_Another_Document_Is_A_Mismatch_And_Writes_Nothing()
    {
        await TestMethod(
            setup: CallerSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new TakeOrder.Command(OpenOrderId, TestLegalDocuments.OtherWorkContractTextId)),
            assert: async (CleansiaDbContext context, BusinessResult<TakeOrder.Response> result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Equal(BusinessErrorMessage.WorkContractTextMismatch, Assert.Single(validation.Errors).Message);
                await AssertNothingWrittenAsync(context, OpenOrderId);
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Cover_Take_Leaves_The_Displaced_Cleaners_Row_And_Names_The_New_Seat()
    {
        await TestMethod(
            setup: CallerSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new TakeOrder.Command(CoverOrderId, TestLegalDocuments.WorkContractTextEnId)),
            assert: async (CleansiaDbContext context, BusinessResult<TakeOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);

                var order = await context.Orders.IgnoreQueryFilters()
                    .Include(o => o.AssignedEmployees)
                    .SingleAsync(o => o.Id == CoverOrderId);
                var newSeat = Assert.Single(order.AssignedEmployees);
                Assert.Equal(CallerEmployeeId, newSeat.EmployeeId);
                Assert.NotEqual(DisplacedSeatId, newSeat.Id);

                var rows = await context.WorkContractAcceptances.IgnoreQueryFilters()
                    .Where(a => a.OrderId == CoverOrderId).OrderBy(a => a.AcceptedOn).ToListAsync();
                Assert.Equal(2, rows.Count);
                Assert.Equal(DisplacedSeatId, rows[0].OrderEmployeeId);
                Assert.Equal(OtherEmployeeId, rows[0].EmployeeId);
                Assert.Equal(newSeat.Id, rows[1].OrderEmployeeId);
                Assert.Equal(CallerEmployeeId, rows[1].EmployeeId);
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Beneficiary_Of_A_Held_Order_Takes_It_With_The_Acceptance()
    {
        await TestMethod(
            setup: CallerSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new TakeOrder.Command(HeldOrderId, TestLegalDocuments.WorkContractTextEnId)),
            assert: async (CleansiaDbContext context, BusinessResult<TakeOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                var seat = Assert.Single(await context.OrderEmployees.IgnoreQueryFilters().Where(oe => oe.OrderId == HeldOrderId).ToListAsync());
                var row = Assert.Single(await context.WorkContractAcceptances.IgnoreQueryFilters().Where(a => a.OrderId == HeldOrderId).ToListAsync());
                Assert.Equal(seat.Id, row.OrderEmployeeId);
            },
            transactional: false);
    }

    /// <summary>
    /// The race the handler's commit-with-catch exists for, at the database: two contexts both stage a
    /// seat and its acceptance; the unique seat index separates them at commit, and the loser's
    /// acceptance rolls back with the seat it was staged beside.
    /// </summary>
    [Fact]
    public async Task The_Seat_Race_Loser_Leaves_No_Acceptance_Row()
    {
        await TestMethod(
            arrange: SeedAsync,
            act: async _ =>
            {
                await using var contextA = NewRaceContext();
                await using var contextB = NewRaceContext();
                var orderA = await contextA.Orders.Include(o => o.AssignedEmployees).SingleAsync(o => o.Id == OpenOrderId);
                var orderB = await contextB.Orders.Include(o => o.AssignedEmployees).SingleAsync(o => o.Id == OpenOrderId);
                var document = await contextA.LegalDocuments.Include(d => d.Texts).AsNoTracking().SingleAsync(d => d.Id == TestLegalDocuments.WorkContractId);

                var seatA = OrderEmployee.Create(orderA, await contextA.Employees.SingleAsync(e => e.Id == CallerEmployeeId));
                var seatB = OrderEmployee.Create(orderB, await contextB.Employees.SingleAsync(e => e.Id == OtherEmployeeId));
                orderA.AddAssignedEmployee(seatA);
                orderB.AddAssignedEmployee(seatB);
                contextA.Add(Acceptance(seatA, CallerEmployeeId, document));
                contextB.Add(Acceptance(seatB, OtherEmployeeId, document));

                var outcomes = await Task.WhenAll(
                    Capture(contextA.CommitAsync(CancellationToken.None)),
                    Capture(contextB.CommitAsync(CancellationToken.None)));

                return (Winners: outcomes.Count(o => o is null), Losers: outcomes.Count(o => o is DbUpdateException));
            },
            assert: async (CleansiaDbContext context, (int Winners, int Losers) outcome) =>
            {
                Assert.Equal(1, outcome.Winners);
                Assert.Equal(1, outcome.Losers);
                var seat = Assert.Single(await context.OrderEmployees.IgnoreQueryFilters().Where(oe => oe.OrderId == OpenOrderId).ToListAsync());
                var row = Assert.Single(await context.WorkContractAcceptances.IgnoreQueryFilters().Where(a => a.OrderId == OpenOrderId).ToListAsync());
                Assert.Equal(seat.Id, row.OrderEmployeeId);
                Assert.Equal(seat.EmployeeId, row.EmployeeId);
            },
            transactional: false);
    }

    private static WorkContractAcceptance Acceptance(OrderEmployee seat, string employeeId, Core.Domain.Legal.LegalDocument document) =>
        WorkContractAcceptance.Create(
            OpenOrderId, seat.Id, employeeId, document.TextFor("en")!, document.Version,
            "cleansia.partner", null, null, null, "{\"orderNumber\":\"ORD-RACE\"}");

    private static async Task<Exception?> Capture(Task task)
    {
        try
        {
            await task;
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private CleansiaDbContext NewRaceContext() =>
        new(new DbContextOptionsBuilder<CleansiaDbContext>()
                .UseNpgsql(Fixture.GetConnectionString())
                .Options,
            new TestUserSessionProvider("seat-race", "seat-race@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));

    private static async Task AssertNothingWrittenAsync(CleansiaDbContext context, string orderId)
    {
        Assert.Empty(await context.OrderEmployees.IgnoreQueryFilters().Where(oe => oe.OrderId == orderId).ToListAsync());
        Assert.Empty(await context.WorkContractAcceptances.IgnoreQueryFilters().Where(a => a.OrderId == orderId).ToListAsync());
        Assert.Empty(await context.EmployeeActionAudits.IgnoreQueryFilters().Where(a => a.OrderId == orderId).ToListAsync());
        var statuses = await context.OrderStatusHistory.IgnoreQueryFilters().Where(s => s.OrderId == orderId).ToListAsync();
        Assert.Equal([OrderStatus.New, OrderStatus.Confirmed], statuses.OrderBy(s => s.Sequence).Select(s => s.Status));
    }

    private static Order NewOrder(string id, DateTime cleaningDateTime, Core.Domain.Legal.LegalDocument document)
    {
        var order = Order.Create(
            customerName: "Contract Customer",
            customerEmail: "contract-customer@cleansia.test",
            customerPhone: "+420777111222",
            customerAddress: Address.Create("Contract St 1", "Praha", "12000", CountryId, latitude: 50.0755, longitude: 14.4378),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CzkId,
            paymentStatus: PaymentStatus.Paid);
        order.Id = id;
        order.UpdateEstimatedTime(120);
        order.SetMaxEmployees(1);
        order.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        order.SetWorkContractDocument(document);
        var created = OrderStatusTrack.Create(OrderStatus.New, order);
        created.Created("seed", DateTimeOffset.UtcNow.AddMinutes(-10));
        order.AddOrderStatus(created);
        var confirmed = OrderStatusTrack.Create(OrderStatus.Confirmed, order);
        confirmed.Created("seed", DateTimeOffset.UtcNow.AddMinutes(-5));
        order.AddOrderStatus(confirmed);
        return order;
    }

    private static Employee NewCleaner(string userId, string email, string employeeId)
    {
        var user = User.CreateWithPassword(
            email, TestUtilities.Constants.TestUserSession.TestUserPassword, "Petra", "Contract", UserProfile.Employee);
        user.Id = userId;
        user.ConfirmEmail();
        user.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        employee.Approve(approvedByUserId: "admin-wctake");
        employee.AssignWorkCountry(CountryId);
        employee.UpdateAddress(Address.Create("Cleaner St 9", "Praha", "12000", CountryId));
        employee.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        return employee;
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}

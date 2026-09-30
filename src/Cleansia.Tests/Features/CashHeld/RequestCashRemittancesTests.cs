using Cleansia.Core.AppServices.Features.CashHeld;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Features.Orders;
using Cleansia.TestUtilities.MockDataFactories.Currencies;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.CashHeld;

/// <summary>
/// Owner ruling 2026-09-28, decision 23: cash a pay-period close could not set off is carried forward, and a
/// cleaner who has carried it longer than the company's number of days (30 by default) is e-mailed a request
/// to hand it over — once per balance, dated from the first close the balance outlived.
/// </summary>
public sealed class RequestCashRemittancesTests
{
    private const string TenantId = "cleansia-cz";
    private const string CleanerId = "emp-cash";
    private const string CzkId = "czk";
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);

    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IAppConfigurationProvider> _configuration = new();
    private readonly Mock<ICashLedgerRepository> _cashLedger = new();
    private readonly Mock<IPayPeriodRepository> _payPeriods = new();
    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly List<CashLedgerEntry> _entries = [];
    private readonly List<PayPeriod> _periods = [];
    private readonly List<(decimal Amount, string Symbol, DateTime CarriedSince, string Language)> _requests = [];

    public RequestCashRemittancesTests()
    {
        _tenants.Setup(r => r.GetAllIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([TenantId]);
        _tenants.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(Tenant.Create(TenantId, "Cleansia CZ s.r.o."));
        _cashLedger
            .Setup(r => r.GetBalancesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _entries
                .GroupBy(e => (e.EmployeeId, e.CurrencyId))
                .Select(g => new CashHeldBalance(g.Key.EmployeeId, "Jana Nováková", g.Key.CurrencyId, "CZK", g.Sum(e => e.Amount)))
                .Where(b => b.Amount != 0m)
                .ToList());
        _cashLedger
            .Setup(r => r.GetForEmployeesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _entries.OrderBy(e => e.OccurredAt).ToList());
        _payPeriods.Setup(r => r.GetQueryable()).Returns(() => _periods.AsQueryable().BuildMock());

        var user = User.CreateWithPassword("jana@cleansia.test", "Password1!", "Jana", "Nováková", UserProfile.Employee, languageCode: "uk");
        var employee = Employee.CreateWithUser(user);
        employee.Id = CleanerId;
        _employees.Setup(r => r.GetByIdAsync(CleanerId, It.IsAny<CancellationToken>())).ReturnsAsync(employee);

        var czk = CurrencyMockFactory.Generate();
        czk.Id = CzkId;
        _currencies.Setup(r => r.GetByIdAsync(CzkId, It.IsAny<CancellationToken>())).ReturnsAsync(czk);

        _email
            .Setup(e => e.SendCashRemittanceRequestEmailAsync(
                "jana@cleansia.test", It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, decimal, string, DateTime, string, CancellationToken>(
                (_, _, amount, symbol, since, language, _) => _requests.Add((amount, symbol, since, language)))
            .ReturnsAsync("message-id");
    }

    [Fact]
    public async Task Cash_Carried_Past_A_Close_Longer_Than_Thirty_Days_Is_Asked_For_Once()
    {
        var collection = Collected(1500m, daysAgo: 50);
        var close = ClosedDaysAgo(40);
        SetOff(1000m, close);

        var first = await Handler().Handle(new RequestCashRemittances.Command(), CancellationToken.None);
        var second = await Handler().Handle(new RequestCashRemittances.Command(), CancellationToken.None);

        Assert.Equal(1, first.Value!.Requested);
        Assert.Equal(0, second.Value!.Requested);
        var request = Assert.Single(_requests);
        Assert.Equal((500m, "Kč", close, "uk"), request);
        Assert.Equal(Now.UtcDateTime, collection.RemittanceRequestedAt);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Cash_Carried_For_Thirty_Days_Or_Fewer_Is_Not_Asked_For_Yet()
    {
        Collected(1500m, daysAgo: 45);
        SetOff(1000m, ClosedDaysAgo(30));

        var result = await Handler().Handle(new RequestCashRemittances.Command(), CancellationToken.None);

        Assert.Equal(0, result.Value!.Requested);
        Assert.Empty(_requests);
    }

    [Fact]
    public async Task Cash_No_Close_Has_Yet_Had_The_Chance_To_Set_Off_Is_Not_Carried()
    {
        Collected(800m, daysAgo: 70);
        SetOff(800m, ClosedDaysAgo(60));
        Collected(400m, daysAgo: 45);

        var result = await Handler().Handle(new RequestCashRemittances.Command(), CancellationToken.None);

        Assert.Equal(0, result.Value!.Requested);
    }

    [Fact]
    public async Task A_Balance_That_Fell_To_Zero_And_Rose_Again_Is_Asked_For_Afresh()
    {
        var earlier = Collected(300m, daysAgo: 120);
        ClosedDaysAgo(100);
        earlier.MarkRemittanceRequested(Now.UtcDateTime.AddDays(-65));
        _entries.Add(CashLedgerEntry.ForRemittance(CleanerId, CzkId, 300m, null, Now.UtcDateTime.AddDays(-60)));
        var later = Collected(250m, daysAgo: 55);
        ClosedDaysAgo(35);

        var result = await Handler().Handle(new RequestCashRemittances.Command(), CancellationToken.None);

        Assert.Equal(1, result.Value!.Requested);
        Assert.Equal(250m, Assert.Single(_requests).Amount);
        Assert.Equal(Now.UtcDateTime, later.RemittanceRequestedAt);
    }

    [Fact]
    public async Task A_Company_That_Allows_Longer_Is_Not_Asked_For_Before_Its_Own_Days()
    {
        _configuration
            .Setup(c => c.GetTenantSettingAsync("cash.remittance_request_days", It.IsAny<CancellationToken>()))
            .ReturnsAsync("60");
        Collected(1500m, daysAgo: 50);
        SetOff(1000m, ClosedDaysAgo(40));

        var result = await Handler().Handle(new RequestCashRemittances.Command(), CancellationToken.None);

        Assert.Equal(0, result.Value!.Requested);
    }

    [Fact]
    public async Task A_Company_Frozen_For_Archive_Is_Left_To_Its_Administrators()
    {
        var frozen = Tenant.Create(TenantId, "Cleansia CZ s.r.o.")
            .RequestWindDown(new DateOnly(2026, 10, 1), "admin", Now.AddDays(-60))
            .Deactivate("admin", Now.AddDays(-50))
            .RequestArchive("admin", Now.AddDays(-1));
        _tenants.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(frozen);
        Collected(1500m, daysAgo: 50);
        SetOff(1000m, ClosedDaysAgo(40));

        var result = await Handler().Handle(new RequestCashRemittances.Command(), CancellationToken.None);

        Assert.Equal(0, result.Value!.Requested);
        _cashLedger.Verify(r => r.GetBalancesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private CashLedgerEntry Collected(decimal amount, int daysAgo)
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder($"order-{Guid.NewGuid():N}", OrderStatus.New);
        order.MarkCashCollected(CleanerId, Now.UtcDateTime.AddDays(-daysAgo), amount);
        var entry = CashLedgerEntry.ForCollection(order);
        _entries.Add(entry);
        return entry;
    }

    private DateTime ClosedDaysAgo(int days)
    {
        var closedAt = Now.UtcDateTime.AddDays(-days);
        var period = PayPeriod.Create(DateOnly.FromDateTime(closedAt.AddDays(-15)), DateOnly.FromDateTime(closedAt.AddDays(-1)))
            .Close("System");
        typeof(PayPeriod).GetProperty(nameof(PayPeriod.ClosedAt))!.GetSetMethod(nonPublic: true)!.Invoke(period, [closedAt]);
        _periods.Add(period);
        return closedAt;
    }

    private void SetOff(decimal invoiceTotal, DateTime closedAt)
    {
        var invoice = PayrollMockFactory.Invoice(
            subTotal: invoiceTotal, employeeId: CleanerId, currencyId: CzkId, generatedAt: closedAt.AddMinutes(1));
        invoice.SetOffCash(_entries.Sum(e => e.Amount));
        _entries.Add(CashLedgerEntry.ForSetOff(invoice));
    }

    private RequestCashRemittances.Handler Handler() => new(
        _tenants.Object,
        _tenantProvider.Object,
        _configuration.Object,
        _cashLedger.Object,
        _payPeriods.Object,
        _employees.Object,
        _currencies.Object,
        _email.Object,
        _unitOfWork.Object,
        new StubTimeProvider(Now),
        NullLogger<RequestCashRemittances.Handler>.Instance);

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

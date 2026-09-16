using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.CompanyLifecycle;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.TestUtilities;
using Moq;

namespace Cleansia.Tests.Features.CompanyLifecycle;

/// <summary>
/// ADR-0064 D3 (TC-LC-ARCH-1) — the request that seals a company. Every live fact of the books is a
/// precondition, refused in the table's order with its own key, the chargeback horizon is counted
/// from the latest card-paid clean by the company's own setting, and a frozen company whose build
/// poisoned is admitted again with no second stamp. The handler's one write is the freeze; the
/// message it records is keyed by the instant it was asked for.
/// </summary>
public sealed class ArchiveCompanyTests
{
    private const string TenantId = "cleansia-sk";
    private const string AdminId = "01ADMIN000000000000000000A";
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly WindDownFrom = new(2026, 8, 1);

    private static readonly CompanySettlementFacts Settled = new(
        OpenOrders: 0, OpenOrdersOnOrAfterWindDownFrom: 0, ActiveTemplates: 0, ActiveMemberships: 0,
        CreditBalances: 0, PendingRefunds: 0, OrdersAwaitingPay: 0, OrdersAwaitingReceipt: 0,
        ReceiptsAwaitingFiscalRegistration: 0, OpenPayPeriods: 0, UnpaidInvoices: 0, UninvoicedPayRows: 0,
        OpenDisputes: 0, LatestCardPaidCleaningDateTime: null);

    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<ICompanySettlementReader> _settlement = new();
    private readonly Mock<IAppConfigurationProvider> _configuration = new();
    private readonly Mock<IAuditContext> _auditContext = new();
    private readonly Mock<IPendingDispatch> _pendingDispatch = new();
    private readonly Mock<IOutboxMessageRepository> _outbox = new();
    private readonly StubTimeProvider _clock = new(Now);

    public ArchiveCompanyTests()
    {
        _tenantProvider.Setup(p => p.GetCurrentTenantId()).Returns(TenantId);
        _settlement.Setup(r => r.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Settled);
        _configuration.Setup(p => p.GetTenantSettingAsync(TenantSettingCatalog.ChargebackHorizonDaysKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
    }

    private Tenant Company(Action<Tenant>? shape = null)
    {
        var tenant = Tenant.Create(TenantId, "Cleansia SK s.r.o.");
        shape?.Invoke(tenant);
        _tenants.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        return tenant;
    }

    private Tenant SettledCompany(Action<Tenant>? shape = null) => Company(t =>
    {
        t.RequestWindDown(WindDownFrom, AdminId, Now.AddDays(-60));
        t.Deactivate(AdminId, Now.AddDays(-45));
        shape?.Invoke(t);
    });

    private void Facts(CompanySettlementFacts facts) =>
        _settlement.Setup(r => r.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(facts);

    private ArchiveCompany.Validator Validator() =>
        new(_tenants.Object, _tenantProvider.Object, _settlement.Object, _configuration.Object, _clock);

    private ArchiveCompany.Handler Handler() => new(
        _tenants.Object,
        _tenantProvider.Object,
        new TestUserSessionProvider(AdminId, "admin@cleansia.test"),
        _auditContext.Object,
        _pendingDispatch.Object,
        _outbox.Object,
        _clock);

    private async Task<string> RefusalAsync()
    {
        var result = await Validator().ValidateAsync(new ArchiveCompany.Command());
        var error = Assert.Single(result.Errors);
        return error.ErrorMessage;
    }

    [Fact]
    public async Task An_Operating_Company_Is_Refused_As_Not_Deactivated()
    {
        Company();

        Assert.Equal(BusinessErrorMessage.CompanyNotDeactivated, await RefusalAsync());
        _settlement.Verify(r => r.ReadAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Deactivated_Company_Nobody_Was_Told_About_Is_Refused()
    {
        Company(t => t.Deactivate(AdminId, Now.AddDays(-1)));

        Assert.Equal(BusinessErrorMessage.CompanyWindDownNotRequested, await RefusalAsync());
    }

    public static TheoryData<CompanySettlementFacts, string> UnsettledFacts => new()
    {
        { Settled with { OpenOrders = 1 }, BusinessErrorMessage.CompanyHasOpenOrders },
        { Settled with { OrdersAwaitingPay = 1 }, BusinessErrorMessage.CompanyHasOrdersAwaitingPay },
        { Settled with { OrdersAwaitingReceipt = 1 }, BusinessErrorMessage.CompanyHasOrdersAwaitingReceipt },
        { Settled with { ReceiptsAwaitingFiscalRegistration = 1 }, BusinessErrorMessage.CompanyHasReceiptsAwaitingFiscalRegistration },
        { Settled with { PendingRefunds = 1 }, BusinessErrorMessage.CompanyHasPendingRefunds },
        { Settled with { ActiveMemberships = 1 }, BusinessErrorMessage.CompanyHasActiveMemberships },
        { Settled with { CreditBalances = 1 }, BusinessErrorMessage.CompanyHasCreditBalances },
        { Settled with { OpenPayPeriods = 1 }, BusinessErrorMessage.CompanyHasOpenPayPeriod },
        { Settled with { UnpaidInvoices = 1 }, BusinessErrorMessage.CompanyHasUnpaidInvoices },
        { Settled with { UninvoicedPayRows = 1 }, BusinessErrorMessage.CompanyHasUninvoicedPay },
        { Settled with { OpenDisputes = 1 }, BusinessErrorMessage.CompanyHasOpenDisputes },
        { Settled with { LatestCardPaidCleaningDateTime = Now.UtcDateTime.AddDays(-100) }, BusinessErrorMessage.CompanyWithinChargebackHorizon },
    };

    [Theory]
    [MemberData(nameof(UnsettledFacts))]
    public async Task Each_Live_Fact_Refuses_With_Its_Own_Key(CompanySettlementFacts facts, string expectedKey)
    {
        SettledCompany();
        Facts(facts);

        Assert.Equal(expectedKey, await RefusalAsync());
    }

    [Fact]
    public async Task The_Facts_Refuse_In_The_Tables_Order_When_Several_Are_Live()
    {
        SettledCompany();
        Facts(Settled with { OpenDisputes = 3, CreditBalances = 2, OrdersAwaitingPay = 1 });

        Assert.Equal(BusinessErrorMessage.CompanyHasOrdersAwaitingPay, await RefusalAsync());
    }

    [Fact]
    public async Task A_Horizon_Override_Of_Zero_Admits_A_Card_Charge_Of_A_Hundred_Days_Ago()
    {
        SettledCompany();
        Facts(Settled with { LatestCardPaidCleaningDateTime = Now.UtcDateTime.AddDays(-100) });
        _configuration.Setup(p => p.GetTenantSettingAsync(TenantSettingCatalog.ChargebackHorizonDaysKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync("0");

        var result = await Validator().ValidateAsync(new ArchiveCompany.Command());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task A_Card_Charge_Past_The_Default_Horizon_Is_Admitted()
    {
        SettledCompany();
        Facts(Settled with { LatestCardPaidCleaningDateTime = Now.UtcDateTime.AddDays(-181) });

        var result = await Validator().ValidateAsync(new ArchiveCompany.Command());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task A_Frozen_Company_Not_Yet_Archived_Is_Admitted_Again()
    {
        SettledCompany(t => t.RequestArchive(AdminId, Now.AddDays(-1)));

        var result = await Validator().ValidateAsync(new ArchiveCompany.Command());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task An_Archived_Company_Is_Refused()
    {
        SettledCompany(t => t.RequestArchive(AdminId, Now.AddDays(-2)).MarkArchived(new string('a', 64), Now.AddDays(-1)));

        Assert.Equal(BusinessErrorMessage.CompanyArchived, await RefusalAsync());
    }

    [Fact]
    public async Task A_Company_Missing_From_The_Registry_Is_Refused_As_Not_Found()
    {
        _tenants.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        Assert.Equal(BusinessErrorMessage.TenantNotFound, await RefusalAsync());
    }

    [Fact]
    public async Task The_Request_Freezes_The_Row_Records_One_Message_And_Audits_The_States()
    {
        var tenant = SettledCompany();

        var result = await Handler().Handle(new ArchiveCompany.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CompanyLifecycleState.Frozen, result.Value!.State);
        Assert.Equal(Now, result.Value.ArchiveRequestedOn);
        Assert.Equal(Now, tenant.ArchiveRequestedOn);
        Assert.Equal(AdminId, tenant.ArchiveRequestedBy);
        Assert.Null(tenant.ArchivedOn);

        var key = MessageKeys.CompanyArchive(TenantId, Now);
        _pendingDispatch.Verify(d => d.Enqueue(
            QueueNames.CompanyArchive,
            It.Is<QueueEnvelope<CompanyArchiveMessage>>(e =>
                e.MessageKey == key && e.TenantId == TenantId && e.Payload.TenantId == TenantId && e.Payload.RequestedOn == Now),
            key), Times.Once);
        _auditContext.Verify(a => a.RecordChange(
            "Tenant",
            TenantId,
            new CompanyLifecycleSnapshot(CompanyLifecycleState.Deactivated, WindDownFrom),
            new CompanyLifecycleSnapshot(CompanyLifecycleState.Frozen, WindDownFrom),
            null), Times.Once);
    }

    [Fact]
    public async Task A_Build_Asked_For_Again_Keeps_The_Freeze_And_Records_A_Fresh_Message_Naming_It()
    {
        var frozenOn = Now.AddDays(-1);
        var tenant = SettledCompany(t => t.RequestArchive("someone-else", frozenOn));

        var result = await Handler().Handle(new ArchiveCompany.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(frozenOn, tenant.ArchiveRequestedOn);
        Assert.Equal("someone-else", tenant.ArchiveRequestedBy);
        _pendingDispatch.Verify(d => d.Enqueue(
            QueueNames.CompanyArchive,
            It.Is<QueueEnvelope<CompanyArchiveMessage>>(e => e.Payload.RequestedOn == frozenOn),
            MessageKeys.CompanyArchive(TenantId, Now)), Times.Once);
    }

    [Fact]
    public async Task A_Build_Already_Asked_For_This_Second_Is_Not_Asked_For_Twice()
    {
        SettledCompany(t => t.RequestArchive(AdminId, Now.AddDays(-1)));
        var key = MessageKeys.CompanyArchive(TenantId, Now);
        _outbox.Setup(o => o.GetByQueueAndKeyAsync(QueueNames.CompanyArchive, key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cleansia.Core.Domain.Outbox.OutboxMessage.Create(QueueNames.CompanyArchive, key, "{}", TenantId));

        var result = await Handler().Handle(new ArchiveCompany.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _pendingDispatch.Verify(d => d.Enqueue(It.IsAny<string>(), It.IsAny<QueueEnvelope<CompanyArchiveMessage>>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task A_Company_Missing_From_The_Registry_Is_Not_Found_By_The_Handler()
    {
        _tenants.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        var result = await Handler().Handle(new ArchiveCompany.Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.TenantNotFound, result.Error!.Message);
        _pendingDispatch.Verify(d => d.Enqueue(It.IsAny<string>(), It.IsAny<QueueEnvelope<CompanyArchiveMessage>>(), It.IsAny<string>()), Times.Never);
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

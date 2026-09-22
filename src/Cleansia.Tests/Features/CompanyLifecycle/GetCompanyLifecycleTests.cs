using Cleansia.Core.AppServices.Features.CompanyLifecycle;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;

namespace Cleansia.Tests.Features.CompanyLifecycle;

/// <summary>
/// ADR-0064 D4 — the page's read: the state, the stamps with their actors named by e-mail, every
/// settlement fact as the reader counts it, and the chargeback horizon counted from the latest
/// card-paid clean by the company's own setting (180 days unless overridden; null with no card clean).
/// </summary>
public sealed class GetCompanyLifecycleTests
{
    private const string TenantId = "cleansia-sk";
    private const string AdminId = "01ADMIN000000000000000000A";
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTime LatestClean = new(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<ICountryConfigurationRepository> _configurations = new();
    private readonly Mock<ICompanySettlementReader> _settlement = new();
    private readonly Mock<IAppConfigurationProvider> _configuration = new();
    private readonly Mock<IUserRepository> _users = new();

    private static readonly CompanySettlementFacts Facts = new(
        OpenOrders: 1, OpenOrdersOnOrAfterWindDownFrom: 2, ActiveTemplates: 3, ActiveMemberships: 4,
        CreditBalances: 5, PendingRefunds: 6, OrdersAwaitingPay: 7, OrdersAwaitingReceipt: 8,
        ReceiptsAwaitingFiscalRegistration: 9, OpenPayPeriods: 10, UnpaidInvoices: 11, UninvoicedPayRows: 12,
        OpenDisputes: 13, LatestCardPaidCleaningDateTime: LatestClean);

    public GetCompanyLifecycleTests()
    {
        _tenantProvider.Setup(p => p.GetCurrentTenantId()).Returns(TenantId);
        _configurations.Setup(r => r.GetDefaultMarketAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create("CZE", "CZK", "cs", 0.21m).AssignOperator("cleansia-cz").SetAsDefaultMarket(true));
        _settlement.Setup(r => r.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Facts);
        _configuration.Setup(p => p.GetTenantSettingAsync(TenantSettingCatalog.ChargebackHorizonDaysKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var admin = UserMockFactory.Generate(new UserMockFactory.UserPartial { Email = "admin@cleansia.test" });
        admin.Id = AdminId;
        _users.Setup(r => r.GetByIdAsync(AdminId, It.IsAny<CancellationToken>())).ReturnsAsync(admin);
    }

    private Tenant Company(Action<Tenant>? shape = null)
    {
        var tenant = Tenant.Create(TenantId, "Cleansia SK s.r.o.");
        shape?.Invoke(tenant);
        _tenants.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        return tenant;
    }

    private GetCompanyLifecycle.Handler Handler() => new(
        _tenants.Object, _tenantProvider.Object, _configurations.Object, _settlement.Object, _configuration.Object, _users.Object);

    [Fact]
    public async Task An_Operating_Company_Reads_Its_Name_State_And_Every_Fact_With_The_Default_Horizon()
    {
        Company();

        var result = await Handler().Handle(new GetCompanyLifecycle.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal("Cleansia SK s.r.o.", dto.Name);
        Assert.Equal(CompanyLifecycleState.Operating, dto.State);
        Assert.False(dto.OperatesDefaultMarket);
        Assert.Null(dto.DeactivatedOn);
        Assert.Null(dto.DeactivatedByEmail);
        Assert.Equal((1, 2, 3, 4, 5, 6, 7), (dto.OpenOrders, dto.OpenOrdersOnOrAfterWindDownFrom, dto.ActiveTemplates, dto.ActiveMemberships, dto.CreditBalances, dto.PendingRefunds, dto.OrdersAwaitingPay));
        Assert.Equal((8, 9, 10, 11, 12, 13), (dto.OrdersAwaitingReceipt, dto.ReceiptsAwaitingFiscalRegistration, dto.OpenPayPeriods, dto.UnpaidInvoices, dto.UninvoicedPayRows, dto.OpenDisputes));
        Assert.Equal(LatestClean.AddDays(180), dto.ChargebackHorizonEndsOn);
    }

    [Fact]
    public async Task The_Companys_Own_Horizon_Override_Counts_From_The_Latest_Card_Paid_Clean()
    {
        Company();
        _configuration.Setup(p => p.GetTenantSettingAsync(TenantSettingCatalog.ChargebackHorizonDaysKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync("30");

        var dto = (await Handler().Handle(new GetCompanyLifecycle.Query(), CancellationToken.None)).Value!;

        Assert.Equal(LatestClean.AddDays(30), dto.ChargebackHorizonEndsOn);
    }

    [Fact]
    public async Task No_Card_Paid_Clean_Means_No_Horizon()
    {
        Company();
        _settlement.Setup(r => r.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Facts with { LatestCardPaidCleaningDateTime = null });

        var dto = (await Handler().Handle(new GetCompanyLifecycle.Query(), CancellationToken.None)).Value!;

        Assert.Null(dto.ChargebackHorizonEndsOn);
    }

    [Fact]
    public async Task The_Company_Holding_The_Default_Market_Says_So()
    {
        Company();
        _configurations.Setup(r => r.GetDefaultMarketAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create("SVK", "EUR", "sk", 0.20m).AssignOperator(TenantId).SetAsDefaultMarket(true));

        var dto = (await Handler().Handle(new GetCompanyLifecycle.Query(), CancellationToken.None)).Value!;

        Assert.True(dto.OperatesDefaultMarket);
    }

    [Fact]
    public async Task A_Deactivated_Wound_Down_Company_Names_Its_Actors_By_Email()
    {
        Company(t => t.RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now).Deactivate(AdminId, Now.AddDays(1)));

        var dto = (await Handler().Handle(new GetCompanyLifecycle.Query(), CancellationToken.None)).Value!;

        Assert.Equal(CompanyLifecycleState.Deactivated, dto.State);
        Assert.Equal(Now.AddDays(1), dto.DeactivatedOn);
        Assert.Equal("admin@cleansia.test", dto.DeactivatedByEmail);
        Assert.Equal(new DateOnly(2026, 10, 1), dto.WindDownFrom);
        Assert.Equal(Now, dto.WindDownRequestedOn);
        Assert.Equal("admin@cleansia.test", dto.WindDownRequestedByEmail);
        Assert.Null(dto.ArchiveRequestedByEmail);
        Assert.Null(dto.ArchiveManifestSha256);
    }

    [Fact]
    public async Task An_Archived_Company_Carries_The_Manifest_Hash_And_Its_Archive_Actor()
    {
        var sha = new string('b', 64);
        Company(t => t.RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now).Deactivate(AdminId, Now).RequestArchive(AdminId, Now.AddDays(200)).MarkArchived(sha, Now.AddDays(201)));

        var dto = (await Handler().Handle(new GetCompanyLifecycle.Query(), CancellationToken.None)).Value!;

        Assert.Equal(CompanyLifecycleState.Archived, dto.State);
        Assert.Equal(sha, dto.ArchiveManifestSha256);
        Assert.Equal(Now.AddDays(201), dto.ArchivedOn);
        Assert.Equal("admin@cleansia.test", dto.ArchiveRequestedByEmail);
    }

    [Fact]
    public void The_Dto_Carries_No_Tenant_Id_And_No_Blob_Path()
    {
        var members = typeof(Cleansia.Core.AppServices.Features.CompanyLifecycle.DTOs.CompanyLifecycleDto)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain(members, name => name.Contains("TenantId", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, name => name.Contains("Blob", StringComparison.OrdinalIgnoreCase) || name.Contains("Path", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, name => name.EndsWith("By", StringComparison.Ordinal));
    }
}

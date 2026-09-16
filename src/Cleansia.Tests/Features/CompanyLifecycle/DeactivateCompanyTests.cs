using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.CompanyLifecycle;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.TestUtilities;
using Moq;

namespace Cleansia.Tests.Features.CompanyLifecycle;

/// <summary>
/// ADR-0064 D1 — the switch on the admin's own company. The validator refuses a frozen company, an
/// already-deactivated one, and the one that holds the default market (C1: every anonymous identity
/// request on every host is scoped to that market's operator); the handler stamps the actor and the
/// instant and records the state before and after for the trail.
/// </summary>
public sealed class DeactivateCompanyTests
{
    private const string TenantId = "cleansia-sk";
    private const string AdminId = "01ADMIN000000000000000000A";
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<ICountryConfigurationRepository> _configurations = new();
    private readonly Mock<IAuditContext> _auditContext = new();
    private readonly Mock<IPendingDispatch> _pendingDispatch = new();
    private readonly Mock<IOutboxMessageRepository> _outbox = new();
    private readonly StubTimeProvider _clock = new(Now);

    public DeactivateCompanyTests()
    {
        _tenantProvider.Setup(p => p.GetCurrentTenantId()).Returns(TenantId);
        _configurations.Setup(r => r.GetDefaultMarketAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create("CZE", "CZK", "cs", 0.21m).AssignOperator("cleansia-cz").SetAsDefaultMarket(true));
    }

    private Tenant Company(Action<Tenant>? shape = null)
    {
        var tenant = Tenant.Create(TenantId, "Cleansia SK s.r.o.");
        shape?.Invoke(tenant);
        _tenants.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        return tenant;
    }

    private DeactivateCompany.Validator Validator() => new(_tenants.Object, _tenantProvider.Object, _configurations.Object);

    private DeactivateCompany.Handler Handler() => new(
        _tenants.Object,
        _tenantProvider.Object,
        new TestUserSessionProvider(AdminId, "admin@cleansia.test"),
        _auditContext.Object,
        _pendingDispatch.Object,
        _outbox.Object,
        _clock);

    [Fact]
    public async Task An_Operating_Company_Not_Holding_The_Default_Market_Passes()
    {
        Company();

        var result = await Validator().ValidateAsync(new DeactivateCompany.Command());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task An_Already_Deactivated_Company_Is_Refused()
    {
        Company(t => t.Deactivate(AdminId, Now));

        var result = await Validator().ValidateAsync(new DeactivateCompany.Command());

        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.CompanyAlreadyDeactivated, error.ErrorMessage);
        Assert.Equal(DeactivateCompany.ErrorCode, error.PropertyName);
    }

    [Fact]
    public async Task A_Frozen_Company_Is_Refused_Before_Anything_Else()
    {
        Company(t => t.RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now).Deactivate(AdminId, Now).RequestArchive(AdminId, Now));

        var result = await Validator().ValidateAsync(new DeactivateCompany.Command());

        Assert.Equal(BusinessErrorMessage.CompanyArchived, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task The_Company_Holding_The_Default_Market_Is_Refused()
    {
        Company();
        _configurations.Setup(r => r.GetDefaultMarketAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create("SVK", "EUR", "sk", 0.20m).AssignOperator(TenantId).SetAsDefaultMarket(true));

        var result = await Validator().ValidateAsync(new DeactivateCompany.Command());

        Assert.Equal(BusinessErrorMessage.CompanyOperatesDefaultMarket, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task No_Flagged_Default_Market_Does_Not_Refuse()
    {
        Company();
        _configurations.Setup(r => r.GetDefaultMarketAsync(It.IsAny<CancellationToken>())).ReturnsAsync((CountryConfiguration?)null);

        var result = await Validator().ValidateAsync(new DeactivateCompany.Command());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task The_Handler_Stamps_The_Actor_And_Instant_And_Records_The_States()
    {
        var tenant = Company();

        var result = await Handler().Handle(new DeactivateCompany.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CompanyLifecycleState.Deactivated, result.Value!.State);
        Assert.True(tenant.IsDeactivated);
        Assert.Equal(AdminId, tenant.DeactivatedBy);
        Assert.Equal(Now, tenant.DeactivatedOn);
        _auditContext.Verify(a => a.RecordChange(
            "Tenant",
            TenantId,
            new CompanyLifecycleSnapshot(CompanyLifecycleState.Operating, null),
            new CompanyLifecycleSnapshot(CompanyLifecycleState.Deactivated, null),
            null), Times.Once);
        _pendingDispatch.Verify(d => d.Enqueue(
            It.IsAny<string>(), It.IsAny<QueueEnvelope<CompanyWindDownMessage>>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task The_Handler_Keeps_The_WindDown_Date_In_Both_Snapshots()
    {
        Company(t => t.RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now));

        var result = await Handler().Handle(new DeactivateCompany.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _auditContext.Verify(a => a.RecordChange(
            "Tenant",
            TenantId,
            new CompanyLifecycleSnapshot(CompanyLifecycleState.WindingDown, new DateOnly(2026, 10, 1)),
            new CompanyLifecycleSnapshot(CompanyLifecycleState.Deactivated, new DateOnly(2026, 10, 1)),
            null), Times.Once);
    }

    [Fact]
    public async Task Closing_The_Door_On_A_Company_With_A_WindDown_Date_Runs_The_Sweep_Again()
    {
        Company(t => t.RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now.AddDays(-7)).StartWindDownRun(Now.AddMinutes(-5)));

        var result = await Handler().Handle(new DeactivateCompany.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var key = MessageKeys.CompanyWindDown(TenantId, Now);
        _pendingDispatch.Verify(d => d.Enqueue(
            QueueNames.CompanyWindDown,
            It.Is<QueueEnvelope<CompanyWindDownMessage>>(e => e.MessageKey == key && e.TenantId == TenantId && e.Payload.TenantId == TenantId),
            key), Times.Once);
    }

    [Fact]
    public async Task A_Company_Missing_From_The_Registry_Is_Not_Found()
    {
        _tenants.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        var result = await Handler().Handle(new DeactivateCompany.Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.TenantNotFound, result.Error!.Message);
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

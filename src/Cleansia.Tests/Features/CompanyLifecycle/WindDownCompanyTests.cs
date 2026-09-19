using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.AdminNotifications;
using Cleansia.Core.AppServices.Features.CompanyLifecycle;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.TestUtilities;
using Moq;

namespace Cleansia.Tests.Features.CompanyLifecycle;

/// <summary>
/// ADR-0064 D2 — the request that announces a company's last day of service and starts the sweep. A
/// date is set once and must not be before today in any of the company's own markets; a request with
/// no date is a re-run, admitted only once a date is set and no run is in flight (a run that died is
/// stale after an hour, TC-LC-WD-5); nothing moves on a frozen company. The handler stamps the row
/// on the first request, tells the company's administrators that once, and records one sweep message
/// under the company's own tenant every time.
/// </summary>
public sealed class WindDownCompanyTests
{
    private const string TenantId = "cleansia-sk";
    private const string AdminId = "01ADMIN000000000000000000A";
    // 23:30 UTC on 15 September is already 16 September in Bratislava (UTC+2).
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 23, 30, 0, TimeSpan.Zero);

    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<ICountryConfigurationRepository> _configurations = new();
    private readonly Mock<IAuditContext> _auditContext = new();
    private readonly Mock<IPendingDispatch> _pendingDispatch = new();
    private readonly Mock<IOutboxMessageRepository> _outbox = new();
    private readonly Mock<IAdminNotifier> _adminNotifier = new();
    private readonly List<AdminEvent> _raised = [];
    private readonly StubTimeProvider _clock = new(Now);

    public WindDownCompanyTests()
    {
        _tenantProvider.Setup(p => p.GetCurrentTenantId()).Returns(TenantId);
        _configurations.Setup(r => r.GetOperatedByAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CountryConfiguration.Create("SVK", "EUR", "sk", 0.20m, timeZoneId: "Europe/Bratislava").AssignOperator(TenantId)]);
        _adminNotifier
            .Setup(n => n.NotifyAsync(It.IsAny<AdminEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AdminEvent, CancellationToken>((e, _) => _raised.Add(e))
            .Returns(Task.CompletedTask);
    }

    private Tenant Company(Action<Tenant>? shape = null)
    {
        var tenant = Tenant.Create(TenantId, "Cleansia SK s.r.o.");
        shape?.Invoke(tenant);
        _tenants.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        return tenant;
    }

    private WindDownCompany.Validator Validator() => new(_tenants.Object, _tenantProvider.Object, _configurations.Object, _clock);

    private WindDownCompany.Handler Handler() => new(
        _tenants.Object,
        _tenantProvider.Object,
        new TestUserSessionProvider(AdminId, "admin@cleansia.test"),
        _auditContext.Object,
        _pendingDispatch.Object,
        _outbox.Object,
        _adminNotifier.Object,
        _clock);

    [Fact]
    public async Task A_Date_Today_In_The_Companys_Earliest_Market_Passes()
    {
        Company();

        var result = await Validator().ValidateAsync(new WindDownCompany.Command(new DateOnly(2026, 9, 16)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task A_Date_Already_Past_In_The_Companys_Earliest_Market_Is_Refused()
    {
        Company();

        var result = await Validator().ValidateAsync(new WindDownCompany.Command(new DateOnly(2026, 9, 15)));

        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.CompanyWindDownDateInPast, error.ErrorMessage);
        Assert.Equal(nameof(WindDownCompany.Command.FromDate), error.PropertyName);
    }

    [Fact]
    public async Task With_No_Market_The_Date_Is_Read_In_Utc()
    {
        Company();
        _configurations.Setup(r => r.GetOperatedByAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await Validator().ValidateAsync(new WindDownCompany.Command(new DateOnly(2026, 9, 15)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task A_First_Request_Without_A_Date_Is_Refused()
    {
        Company();

        var result = await Validator().ValidateAsync(new WindDownCompany.Command(null));

        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.Required, error.ErrorMessage);
        Assert.Equal(nameof(WindDownCompany.Command.FromDate), error.PropertyName);
    }

    [Fact]
    public async Task A_Second_Date_Is_Refused_Because_The_Date_Is_Set_Once()
    {
        Company(t => t.RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now));

        var result = await Validator().ValidateAsync(new WindDownCompany.Command(new DateOnly(2026, 11, 1)));

        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.CompanyWindDownAlreadyRequested, error.ErrorMessage);
        Assert.Equal(nameof(WindDownCompany.Command.FromDate), error.PropertyName);
    }

    [Fact]
    public async Task A_Re_Run_Is_Admitted_Once_A_Date_Is_Set_And_Nothing_Runs()
    {
        Company(t => t.RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now).RecordWindDownRun(Now.AddHours(-3)));

        var result = await Validator().ValidateAsync(new WindDownCompany.Command(null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task A_Re_Run_Is_Refused_While_A_Run_Started_Ten_Minutes_Ago()
    {
        Company(t => t.RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now).StartWindDownRun(Now.AddMinutes(-10)));

        var result = await Validator().ValidateAsync(new WindDownCompany.Command(null));

        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.CompanyWindDownInProgress, error.ErrorMessage);
        Assert.Equal(WindDownCompany.ErrorCode, error.PropertyName);
    }

    [Fact]
    public async Task A_Re_Run_Is_Admitted_When_The_Running_Stamp_Is_Two_Hours_Old()
    {
        Company(t => t.RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now).StartWindDownRun(Now.AddHours(-2)));

        var result = await Validator().ValidateAsync(new WindDownCompany.Command(null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task A_Frozen_Company_Is_Refused_Before_Anything_Else()
    {
        Company(t => t.RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now).Deactivate(AdminId, Now).RequestArchive(AdminId, Now));

        var result = await Validator().ValidateAsync(new WindDownCompany.Command(null));

        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.CompanyArchived, error.ErrorMessage);
        Assert.Equal(WindDownCompany.ErrorCode, error.PropertyName);
    }

    [Fact]
    public async Task A_Company_Missing_From_The_Registry_Is_Refused_As_Not_Found_Not_As_Archived()
    {
        _tenants.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        var result = await Validator().ValidateAsync(new WindDownCompany.Command(null));

        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.TenantNotFound, error.ErrorMessage);
        Assert.Equal(WindDownCompany.ErrorCode, error.PropertyName);
    }

    [Fact]
    public async Task A_Deactivated_Company_May_Still_Post_Its_First_Date()
    {
        Company(t => t.Deactivate(AdminId, Now));

        var result = await Validator().ValidateAsync(new WindDownCompany.Command(new DateOnly(2026, 9, 16)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task The_First_Request_Stamps_The_Row_Records_One_Message_And_Audits_The_States()
    {
        var tenant = Company();
        var from = new DateOnly(2026, 10, 1);

        var result = await Handler().Handle(new WindDownCompany.Command(from), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CompanyLifecycleState.WindingDown, result.Value!.State);
        Assert.Equal(from, result.Value.WindDownFrom);
        Assert.Equal(from, tenant.WindDownFrom);
        Assert.Equal(AdminId, tenant.WindDownRequestedBy);
        Assert.Equal(Now, tenant.WindDownRequestedOn);

        var key = MessageKeys.CompanyWindDown(TenantId, Now);
        _pendingDispatch.Verify(d => d.Enqueue(
            QueueNames.CompanyWindDown,
            It.Is<QueueEnvelope<CompanyWindDownMessage>>(e => e.MessageKey == key && e.TenantId == TenantId && e.Payload.TenantId == TenantId),
            key), Times.Once);
        _auditContext.Verify(a => a.RecordChange(
            "Tenant",
            TenantId,
            new CompanyLifecycleSnapshot(CompanyLifecycleState.Operating, null),
            new CompanyLifecycleSnapshot(CompanyLifecycleState.WindingDown, from),
            null), Times.Once);
    }

    [Fact]
    public async Task The_First_Request_Tells_The_Administrators_Once_With_The_Date_And_The_Request_Instant_As_Subject()
    {
        Company();
        var from = new DateOnly(2026, 10, 1);

        var result = await Handler().Handle(new WindDownCompany.Command(from), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var raised = Assert.Single(_raised);
        Assert.Equal(AdminNotificationEventCatalog.CompanyWindDownRequested, raised.Key);
        Assert.Equal(TenantId, raised.TenantId);
        Assert.Equal($"{TenantId}:{Now.UtcDateTime:yyyyMMddHHmmss}", raised.Subject);
        var declared = AdminEventCatalog.Find(AdminNotificationEventCatalog.CompanyWindDownRequested).EmailArgOrder;
        Assert.Equal(declared.OrderBy(a => a), raised.Args.Keys.OrderBy(a => a));
        Assert.Equal("2026-10-01", raised.Args["windDownFrom"]);
    }

    [Fact]
    public async Task A_Re_Run_Tells_Nobody()
    {
        Company(t => t.RequestWindDown(new DateOnly(2026, 10, 1), "someone-else", Now.AddDays(-7)));

        var result = await Handler().Handle(new WindDownCompany.Command(null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_raised);
    }

    [Fact]
    public async Task A_Re_Run_Leaves_The_Stamps_Alone_And_Records_A_Fresh_Message()
    {
        var requestedOn = Now.AddDays(-7);
        var tenant = Company(t => t.RequestWindDown(new DateOnly(2026, 10, 1), "someone-else", requestedOn));

        var result = await Handler().Handle(new WindDownCompany.Command(null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("someone-else", tenant.WindDownRequestedBy);
        Assert.Equal(requestedOn, tenant.WindDownRequestedOn);
        _pendingDispatch.Verify(d => d.Enqueue(
            QueueNames.CompanyWindDown,
            It.IsAny<QueueEnvelope<CompanyWindDownMessage>>(),
            MessageKeys.CompanyWindDown(TenantId, Now)), Times.Once);
    }

    [Fact]
    public async Task A_Run_Already_Asked_For_This_Second_Is_Not_Asked_For_Twice()
    {
        Company(t => t.RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now.AddDays(-7)));
        var key = MessageKeys.CompanyWindDown(TenantId, Now);
        _outbox.Setup(o => o.GetByQueueAndKeyAsync(QueueNames.CompanyWindDown, key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cleansia.Core.Domain.Outbox.OutboxMessage.Create(QueueNames.CompanyWindDown, key, "{}", TenantId));

        var result = await Handler().Handle(new WindDownCompany.Command(null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _pendingDispatch.Verify(d => d.Enqueue(It.IsAny<string>(), It.IsAny<QueueEnvelope<CompanyWindDownMessage>>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task A_Company_Missing_From_The_Registry_Is_Not_Found()
    {
        _tenants.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        var result = await Handler().Handle(new WindDownCompany.Command(new DateOnly(2026, 10, 1)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.TenantNotFound, result.Error!.Message);
        _pendingDispatch.Verify(d => d.Enqueue(It.IsAny<string>(), It.IsAny<QueueEnvelope<CompanyWindDownMessage>>(), It.IsAny<string>()), Times.Never);
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

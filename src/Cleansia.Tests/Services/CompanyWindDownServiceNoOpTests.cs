using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// The permanent no-ops of the wind-down sweep (ADR-0064 D2, C20): a message naming a company that
/// does not exist, that has no wind-down date, or that is frozen for archive is discarded with a log
/// and writes nothing — not even the run stamp — so a stale message can never touch sealed books. The
/// steps themselves run on Postgres in <c>CompanyWindDownSweepTests</c>.
/// </summary>
public sealed class CompanyWindDownServiceNoOpTests
{
    private const string TenantId = "cleansia-sk";
    private const string AdminId = "01ADMIN000000000000000000A";
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserRepository> _users = new(MockBehavior.Strict);
    private readonly Mock<IQueueClient> _queue = new(MockBehavior.Strict);
    private readonly Mock<IOrderRepository> _orders = new(MockBehavior.Strict);

    private CompanyWindDownService Service() => new(
        _tenants.Object,
        _tenantProvider.Object,
        _unitOfWork.Object,
        _users.Object,
        _queue.Object,
        Mock.Of<ICampaignProgressStore>(),
        _orders.Object,
        Mock.Of<ICountryConfigurationRepository>(),
        Mock.Of<IPlatformOrderCancellation>(),
        Mock.Of<IRecurringBookingTemplateRepository>(),
        Mock.Of<IUserMembershipRepository>(),
        Mock.Of<IStripeClient>(),
        Mock.Of<ICreditAccountRepository>(),
        Mock.Of<IPayPeriodRepository>(),
        Mock.Of<IPayPeriodBackgroundService>(),
        new StubTimeProvider(Now),
        NullLogger<CompanyWindDownService>.Instance);

    private void Company(Action<Tenant>? shape)
    {
        var tenant = Tenant.Create(TenantId, "Cleansia SK s.r.o.");
        shape?.Invoke(tenant);
        _tenants.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
    }

    [Fact]
    public async Task A_Company_Missing_From_The_Registry_Is_A_Permanent_NoOp()
    {
        _tenants.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        var summary = await Service().RunAsync(TenantId, CancellationToken.None);

        Assert.False(summary.Ran);
        Assert.NotNull(summary.SkippedBecause);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Company_Without_A_WindDown_Date_Is_A_Permanent_NoOp()
    {
        Company(t => t.Deactivate(AdminId, Now));

        var summary = await Service().RunAsync(TenantId, CancellationToken.None);

        Assert.False(summary.Ran);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Frozen_Company_Is_A_Permanent_NoOp()
    {
        Company(t => t.RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now).Deactivate(AdminId, Now).RequestArchive(AdminId, Now));

        var summary = await Service().RunAsync(TenantId, CancellationToken.None);

        Assert.False(summary.Ran);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task The_Sweep_Scopes_Itself_To_The_Company_Before_Its_First_Read()
    {
        _tenants.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        await Service().RunAsync(TenantId, CancellationToken.None);

        _tenantProvider.Verify(p => p.SetTenantOverride(TenantId), Times.Once);
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.CompanyLifecycle;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Moq;

namespace Cleansia.Tests.Features.CompanyLifecycle;

/// <summary>
/// ADR-0064 D5 — reopening is admitted from <c>Deactivated</c> only: an operating company has nothing
/// to reopen, a frozen or archived one is never reopened here. The handler clears the deactivation and
/// the wind-down stamps and records the states before and after.
/// </summary>
public sealed class ReactivateCompanyTests
{
    private const string TenantId = "cleansia-sk";
    private const string AdminId = "01ADMIN000000000000000000A";
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IAuditContext> _auditContext = new();

    public ReactivateCompanyTests()
    {
        _tenantProvider.Setup(p => p.GetCurrentTenantId()).Returns(TenantId);
    }

    private Tenant Company(Action<Tenant>? shape = null)
    {
        var tenant = Tenant.Create(TenantId, "Cleansia SK s.r.o.");
        shape?.Invoke(tenant);
        _tenants.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        return tenant;
    }

    private ReactivateCompany.Validator Validator() => new(_tenants.Object, _tenantProvider.Object);

    private ReactivateCompany.Handler Handler() => new(_tenants.Object, _tenantProvider.Object, _auditContext.Object);

    [Fact]
    public async Task A_Deactivated_Company_Passes()
    {
        Company(t => t.Deactivate(AdminId, Now));

        Assert.True((await Validator().ValidateAsync(new ReactivateCompany.Command())).IsValid);
    }

    [Fact]
    public async Task An_Operating_Company_Is_Refused()
    {
        Company();

        var result = await Validator().ValidateAsync(new ReactivateCompany.Command());

        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.CompanyNotDeactivated, error.ErrorMessage);
        Assert.Equal(ReactivateCompany.ErrorCode, error.PropertyName);
    }

    [Fact]
    public async Task A_Frozen_Company_Is_Refused_As_Archived()
    {
        Company(t => t.RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now).Deactivate(AdminId, Now).RequestArchive(AdminId, Now));

        var result = await Validator().ValidateAsync(new ReactivateCompany.Command());

        Assert.Equal(BusinessErrorMessage.CompanyArchived, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task The_Handler_Reopens_The_Company_And_Records_The_States()
    {
        var tenant = Company(t => t.RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now).Deactivate(AdminId, Now));

        var result = await Handler().Handle(new ReactivateCompany.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CompanyLifecycleState.Operating, result.Value!.State);
        Assert.True(tenant.IsActive);
        Assert.Null(tenant.DeactivatedOn);
        Assert.Null(tenant.WindDownFrom);
        _auditContext.Verify(a => a.RecordChange(
            "Tenant",
            TenantId,
            new CompanyLifecycleSnapshot(CompanyLifecycleState.Deactivated, new DateOnly(2026, 10, 1)),
            new CompanyLifecycleSnapshot(CompanyLifecycleState.Operating, null),
            null), Times.Once);
    }

    [Fact]
    public async Task A_Company_Missing_From_The_Registry_Is_Not_Found()
    {
        _tenants.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        var result = await Handler().Handle(new ReactivateCompany.Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.TenantNotFound, result.Error!.Message);
    }
}

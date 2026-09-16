using Cleansia.Core.Domain.Tenancy;

namespace Cleansia.Tests.Domain.Tenancy;

/// <summary>
/// The company's lifecycle is a small state machine on its own registry row (ADR-0064 D1): every
/// transition refuses what the state does not admit, the predicates read the stamps, and the state is
/// the highest that applies.
/// </summary>
public sealed class TenantLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);
    private const string AdminId = "01ADMIN000000000000000000A";

    private static Tenant Operating() => Tenant.Create("cleansia-sk", "Cleansia SK s.r.o.");

    [Fact]
    public void A_Fresh_Company_Is_Operating_And_Carries_The_Seeds_Creation_Stamp()
    {
        var tenant = Operating();

        Assert.Equal(CompanyLifecycleState.Operating, tenant.State);
        Assert.True(tenant.IsActive);
        Assert.False(tenant.IsDeactivated);
        Assert.False(tenant.IsWindDownRequested);
        Assert.False(tenant.IsFrozen);
        Assert.False(tenant.IsArchived);
        Assert.False(tenant.IsWindDownRunning(Now));
        Assert.Equal("seed", tenant.CreatedBy);
    }

    [Fact]
    public void Deactivate_Stamps_The_Actor_And_Instant_And_Clears_IsActive()
    {
        var tenant = Operating();

        tenant.Deactivate(AdminId, Now);

        Assert.True(tenant.IsDeactivated);
        Assert.False(tenant.IsActive);
        Assert.Equal(AdminId, tenant.DeactivatedBy);
        Assert.Equal(Now, tenant.DeactivatedOn);
        Assert.Equal(CompanyLifecycleState.Deactivated, tenant.State);
    }

    [Fact]
    public void Deactivate_Twice_Is_Refused()
    {
        var tenant = Operating().Deactivate(AdminId, Now);

        Assert.Throws<InvalidOperationException>(() => tenant.Deactivate(AdminId, Now.AddMinutes(1)));
    }

    [Fact]
    public void Reactivate_Clears_The_Deactivation_And_Every_WindDown_Stamp()
    {
        var tenant = Operating()
            .RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now)
            .StartWindDownRun(Now.AddMinutes(1))
            .RecordWindDownRun(Now.AddMinutes(2))
            .Deactivate(AdminId, Now.AddMinutes(3));

        tenant.Reactivate();

        Assert.True(tenant.IsActive);
        Assert.Null(tenant.DeactivatedBy);
        Assert.Null(tenant.DeactivatedOn);
        Assert.Null(tenant.WindDownFrom);
        Assert.Null(tenant.WindDownRequestedOn);
        Assert.Null(tenant.WindDownRequestedBy);
        Assert.Null(tenant.WindDownRunStartedOn);
        Assert.Null(tenant.WindDownLastRunOn);
        Assert.Equal(CompanyLifecycleState.Operating, tenant.State);
    }

    [Fact]
    public void Reactivate_On_An_Operating_Company_Is_Refused()
    {
        Assert.Throws<InvalidOperationException>(() => Operating().Reactivate());
    }

    [Fact]
    public void A_WindDown_Request_Stamps_The_Date_Actor_And_Instant_Once()
    {
        var tenant = Operating();

        tenant.RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now);

        Assert.True(tenant.IsWindDownRequested);
        Assert.Equal(new DateOnly(2026, 10, 1), tenant.WindDownFrom);
        Assert.Equal(AdminId, tenant.WindDownRequestedBy);
        Assert.Equal(Now, tenant.WindDownRequestedOn);
        Assert.Equal(CompanyLifecycleState.WindingDown, tenant.State);
        Assert.Throws<InvalidOperationException>(() => tenant.RequestWindDown(new DateOnly(2026, 11, 1), AdminId, Now));
    }

    [Fact]
    public void A_Run_Is_Running_Until_Recorded_Or_Stale_By_An_Hour()
    {
        var tenant = Operating().StartWindDownRun(Now);

        Assert.True(tenant.IsWindDownRunning(Now.AddMinutes(59)));
        Assert.False(tenant.IsWindDownRunning(Now.AddMinutes(60)));

        tenant.RecordWindDownRun(Now.AddMinutes(5));

        Assert.Null(tenant.WindDownRunStartedOn);
        Assert.Equal(Now.AddMinutes(5), tenant.WindDownLastRunOn);
        Assert.False(tenant.IsWindDownRunning(Now.AddMinutes(6)));
    }

    [Fact]
    public void An_Archive_Request_Needs_A_Deactivated_Wound_Down_Company_And_Freezes_It()
    {
        Assert.Throws<InvalidOperationException>(() => Operating().RequestArchive(AdminId, Now));
        Assert.Throws<InvalidOperationException>(() => Operating().Deactivate(AdminId, Now).RequestArchive(AdminId, Now));
        Assert.Throws<InvalidOperationException>(() =>
            Operating().RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now).RequestArchive(AdminId, Now));

        var tenant = Operating()
            .RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now)
            .Deactivate(AdminId, Now)
            .RequestArchive(AdminId, Now.AddDays(200));

        Assert.True(tenant.IsFrozen);
        Assert.False(tenant.IsArchived);
        Assert.Equal(AdminId, tenant.ArchiveRequestedBy);
        Assert.Equal(Now.AddDays(200), tenant.ArchiveRequestedOn);
        Assert.Equal(CompanyLifecycleState.Frozen, tenant.State);
    }

    [Fact]
    public void A_Frozen_Company_Admits_No_Lifecycle_Change_But_The_Archive_Completion()
    {
        var tenant = Operating()
            .RequestWindDown(new DateOnly(2026, 10, 1), AdminId, Now)
            .Deactivate(AdminId, Now)
            .RequestArchive(AdminId, Now);

        Assert.Throws<InvalidOperationException>(() => tenant.Reactivate());
        Assert.Throws<InvalidOperationException>(() => tenant.Deactivate(AdminId, Now));
        Assert.Throws<InvalidOperationException>(() => tenant.RequestWindDown(new DateOnly(2026, 12, 1), AdminId, Now));
        Assert.Throws<InvalidOperationException>(() => tenant.RequestArchive(AdminId, Now));

        var sha = new string('a', 64);
        tenant.MarkArchived(sha, Now.AddHours(1));

        Assert.True(tenant.IsArchived);
        Assert.Equal(sha, tenant.ArchiveManifestSha256);
        Assert.Equal(Now.AddHours(1), tenant.ArchivedOn);
        Assert.Equal(CompanyLifecycleState.Archived, tenant.State);
    }

    [Fact]
    public void MarkArchived_Without_A_Request_Is_Refused()
    {
        Assert.Throws<InvalidOperationException>(() => Operating().MarkArchived(new string('a', 64), Now));
    }
}

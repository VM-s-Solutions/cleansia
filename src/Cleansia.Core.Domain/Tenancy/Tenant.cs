using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Tenancy;

/// <summary>
/// An operating company under the holding — the legal entity that contracts the customer, employs
/// the cleaner, issues the receipt and pays the payout (ADR-0061 D1). Tenantless by construction: it
/// is the thing every <c>TenantId</c> column names. The registry (which companies exist) is seed-only;
/// the row's lifecycle — deactivated, winding down, frozen, archived — is driven by the company's own
/// administrators (ADR-0064). The id is assigned, not generated ("cleansia-cz"), and capped at 26
/// because every <c>TenantId</c> column is varchar(26).
/// </summary>
public class Tenant : Auditable
{
    private static readonly TimeSpan WindDownRunStaleness = TimeSpan.FromHours(1);

    [Required]
    [MaxLength(26)]
    public override string Id { get; set; } = default!;

    [Required]
    [MaxLength(200)]
    public string Name { get; private set; } = default!;

    public DateOnly? WindDownFrom { get; private set; }

    public DateTimeOffset? WindDownRequestedOn { get; private set; }

    [MaxLength(26)]
    public string? WindDownRequestedBy { get; private set; }

    public DateTimeOffset? WindDownRunStartedOn { get; private set; }

    public DateTimeOffset? WindDownLastRunOn { get; private set; }

    public DateTimeOffset? ArchiveRequestedOn { get; private set; }

    [MaxLength(26)]
    public string? ArchiveRequestedBy { get; private set; }

    public DateTimeOffset? ArchivedOn { get; private set; }

    [MaxLength(64)]
    public string? ArchiveManifestSha256 { get; private set; }

    public bool IsDeactivated => !IsActive;

    public bool IsWindDownRequested => WindDownFrom is not null;

    public bool IsFrozen => ArchiveRequestedOn is not null;

    public bool IsArchived => ArchivedOn is not null;

    // A run that died without recording its end would otherwise block every later run for good.
    public bool IsWindDownRunning(DateTimeOffset now) =>
        WindDownRunStartedOn is { } startedOn && startedOn > now - WindDownRunStaleness;

    public CompanyLifecycleState State =>
        IsArchived ? CompanyLifecycleState.Archived
        : IsFrozen ? CompanyLifecycleState.Frozen
        : IsDeactivated ? CompanyLifecycleState.Deactivated
        : IsWindDownRequested ? CompanyLifecycleState.WindingDown
        : CompanyLifecycleState.Operating;

    private Tenant() { }

    public static Tenant Create(string id, string name)
    {
        var tenant = new Tenant { Id = id, Name = name };
        tenant.Created("seed", DateTimeOffset.UtcNow);
        return tenant;
    }

    public Tenant Deactivate(string byUserId, DateTimeOffset now)
    {
        RefuseWhenFrozen();
        if (IsDeactivated)
        {
            throw new InvalidOperationException($"Company {Id} is already deactivated.");
        }

        Deactivated(byUserId, now);
        return this;
    }

    public Tenant Reactivate()
    {
        RefuseWhenFrozen();
        if (!IsDeactivated)
        {
            throw new InvalidOperationException($"Company {Id} is not deactivated.");
        }

        Reactivated();
        WindDownFrom = null;
        WindDownRequestedOn = null;
        WindDownRequestedBy = null;
        WindDownRunStartedOn = null;
        WindDownLastRunOn = null;
        return this;
    }

    public Tenant RequestWindDown(DateOnly from, string byUserId, DateTimeOffset now)
    {
        RefuseWhenFrozen();
        if (IsWindDownRequested)
        {
            throw new InvalidOperationException($"Company {Id} already has a wind-down date.");
        }

        WindDownFrom = from;
        WindDownRequestedOn = now;
        WindDownRequestedBy = byUserId;
        return this;
    }

    public Tenant StartWindDownRun(DateTimeOffset now)
    {
        WindDownRunStartedOn = now;
        return this;
    }

    public Tenant RecordWindDownRun(DateTimeOffset now)
    {
        WindDownRunStartedOn = null;
        WindDownLastRunOn = now;
        return this;
    }

    public Tenant RequestArchive(string byUserId, DateTimeOffset now)
    {
        RefuseWhenFrozen();
        if (!IsDeactivated || !IsWindDownRequested)
        {
            throw new InvalidOperationException($"Company {Id} must be deactivated and wound down before it is archived.");
        }

        ArchiveRequestedOn = now;
        ArchiveRequestedBy = byUserId;
        return this;
    }

    public Tenant MarkArchived(string manifestSha256, DateTimeOffset now)
    {
        if (!IsFrozen)
        {
            throw new InvalidOperationException($"Company {Id} has no archive request to complete.");
        }

        ArchivedOn = now;
        ArchiveManifestSha256 = manifestSha256;
        return this;
    }

    private void RefuseWhenFrozen()
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException($"Company {Id} is frozen for archive; its lifecycle cannot change.");
        }
    }
}

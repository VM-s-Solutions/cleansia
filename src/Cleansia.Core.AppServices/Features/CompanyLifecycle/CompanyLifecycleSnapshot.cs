using Cleansia.Core.Domain.Tenancy;

namespace Cleansia.Core.AppServices.Features.CompanyLifecycle;

/// <summary>
/// What a lifecycle act records before and after itself in the admin audit trail: the state and the
/// wind-down date. Ids and outcomes only, no person (ADR-0012 D4.1).
/// </summary>
public sealed record CompanyLifecycleSnapshot(CompanyLifecycleState State, DateOnly? WindDownFrom)
{
    public static CompanyLifecycleSnapshot Of(Tenant tenant) => new(tenant.State, tenant.WindDownFrom);
}

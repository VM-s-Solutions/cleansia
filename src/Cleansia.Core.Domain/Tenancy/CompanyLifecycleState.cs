using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Tenancy;

/// <summary>
/// Where a company stands in its lifecycle (ADR-0064 D4), the highest that applies: a frozen company
/// is also deactivated, an archived one is also frozen.
/// </summary>
[SwaggerEnumAsInt]
public enum CompanyLifecycleState
{
    Operating = 1,
    WindingDown = 2,
    Deactivated = 3,
    Frozen = 4,
    Archived = 5,
}

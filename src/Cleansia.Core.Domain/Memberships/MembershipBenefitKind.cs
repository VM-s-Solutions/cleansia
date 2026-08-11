using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Memberships;

/// <summary>
/// ADR-0035 D5 — which metered membership benefit a <see cref="MembershipBenefitUsage"/> row counts.
/// Of the five Plus perks only the express waiver is countable; the discriminator exists because an int
/// column with one value is nearly free and removing it later costs a migration.
/// <para>Never reorder — the integers are persisted.</para>
/// </summary>
[SwaggerEnumAsInt]
public enum MembershipBenefitKind
{
    /// <summary>One granted express-surcharge waiver (ADR-0035 D1).</summary>
    ExpressUpgrade = 1,
}

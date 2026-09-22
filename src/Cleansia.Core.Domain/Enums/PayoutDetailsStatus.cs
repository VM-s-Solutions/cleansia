using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

/// <summary>
/// ADR-0034 D5. Presence of the record makes a profile complete; only <see cref="Provided"/> lets a
/// payout invoice be approved (D7 — the gate sits at approval, not issuance, since 2026-09-12).
/// <para>Never reorder — the integers are persisted.</para>
/// </summary>
[SwaggerEnumAsInt]
public enum PayoutDetailsStatus
{
    /// <summary>Details are present and passed the real validator (ADR-0034 D4).</summary>
    Provided = 1,

    /// <summary>Details are present but unusable for payout; the cleaner must supply them again.</summary>
    NeedsReconfirmation = 2,
}

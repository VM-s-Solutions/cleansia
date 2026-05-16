using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.AppServices.Shared.DTOs.Enums;

/// <summary>
/// Which discount source(s) applied to a given order or quote.
///
/// Pre-LOY-003 (best-of-three): exactly one of <see cref="Tier"/>,
/// <see cref="Membership"/>, <see cref="Promo"/> applied per order, mutually
/// exclusive. <see cref="Combined"/> didn't exist.
///
/// Post-LOY-003 (additive Plus + Tier, cap 12%): Plus (Membership) and tier
/// can stack additively up to a 12% cap on the combined amount; a valid
/// promo replaces the combined value if the promo is larger. The enum value
/// reflects what actually applied:
///
///   * <see cref="Tier"/> — only tier (user has no Plus, no promo).
///   * <see cref="Membership"/> — only Plus (tier was 0% or below the 1000
///     CZK floor; no promo or promo didn't win).
///   * <see cref="Combined"/> — both Plus AND tier applied, additive with cap.
///   * <see cref="Promo"/> — promo won over the combined (or over a single source).
///   * <see cref="None"/> — no discount source applied.
///
/// Historical orders persisted before LOY-003 keep their original single-source
/// enum value — the per-amount fields on the Order entity are the authoritative
/// snapshot. Mappers re-derive this enum from those fields, so the
/// <see cref="Combined"/> value never appears on pre-LOY-003 orders.
/// </summary>
[SwaggerEnumAsInt]
public enum AppliedDiscountSource
{
    None = 0,
    Tier = 1,
    Membership = 2,
    Promo = 3,
    Combined = 4,
}

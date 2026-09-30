using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

/// <summary>
/// How the customer wants a justified complaint settled, stated when they file it. A card refund unless
/// they chose credit (owner ruling 2026-09-28): credit expires and is never paid out, so it is never the
/// settlement a customer did not ask for.
/// </summary>
[SwaggerEnumAsInt]
public enum DisputeSettlementPreference
{
    CardRefund = 1,
    Credit = 2,
}

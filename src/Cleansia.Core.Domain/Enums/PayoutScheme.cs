using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

/// <summary>
/// ADR-0034 D1 — the axis payout details generalize along: how a bank account is identified in the
/// bank's own country. Adding a country that shares an existing scheme is a
/// <c>CountryConfiguration.PayoutScheme</c> seed value, not a schema change.
/// <para>Never reorder — the integers are persisted.</para>
/// </summary>
[SwaggerEnumAsInt]
public enum PayoutScheme
{
    /// <summary>
    /// CZ/SK domestic: (optional prefix, account number, bank code), from which the IBAN is derived
    /// server-side (ADR-0034 D5.2). SK shares the Czechoslovak numbering structure, so it is the same
    /// scheme with a different seed value.
    /// </summary>
    CzskDomesticWithIban = 1,

    /// <summary>A self-describing IBAN with no domestic decomposition.</summary>
    SepaIban = 2,

    /// <summary>
    /// A tokenised destination held by a payment provider. Only <c>ProviderAccountRef</c> is populated —
    /// an id, never a card number (ADR-0034 D9a).
    /// </summary>
    ProviderPayoutToken = 3,
}

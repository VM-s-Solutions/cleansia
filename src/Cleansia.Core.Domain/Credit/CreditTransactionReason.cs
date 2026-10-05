using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Credit;

/// <summary>
/// Why a customer's credit balance moved. Server-owned and closed, for the same reason
/// <c>DisputeReason</c> and <c>ReviewTag</c> are: "what did we give credit for last month" is a
/// question the platform should answer without parsing prose.
/// </summary>
[SwaggerEnumAsInt]
public enum CreditTransactionReason
{
    /// <summary>A dispute settled in credit, because the customer chose credit over a card refund on filing.</summary>
    DisputeSettlement = 1,

    /// <summary>The cleaner cancelled or did not arrive. The amount is <c>Currency.NoShowCredit</c>, authored per currency.</summary>
    CleanerNoShow = 2,

    /// <summary>An admin issued goodwill credit outside any dispute.</summary>
    Goodwill = 3,

    /// <summary>
    /// A referral qualified: both the inviter and the invited friend are paid. The amount is
    /// <c>Currency.ReferralCredit</c>, authored per currency.
    /// </summary>
    Referral = 4,

    /// <summary>Spent against an order at checkout.</summary>
    OrderPayment = 10,

    /// <summary>Returned to the balance because the order it paid for was refunded or cancelled.</summary>
    OrderPaymentReturned = 11,

    /// <summary>
    /// Taken because it expired, an admin discharged it, or account erasure forfeited it.
    /// The ledger note records which reason applied.
    /// </summary>
    Expired = 12,

    /// <summary>
    /// An admin reversed a qualified referral and took back what its <see cref="Referral"/> grant gave,
    /// no more than the balance still held.
    /// </summary>
    ReferralReversed = 13,
}

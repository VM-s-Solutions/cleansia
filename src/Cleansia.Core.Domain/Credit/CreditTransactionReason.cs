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
    /// <summary>An admin settled a dispute with credit instead of, or alongside, a card refund.</summary>
    DisputeSettlement = 1,

    /// <summary>The cleaner cancelled or did not arrive. → BookingPolicy.NoShowCreditCzk</summary>
    CleanerNoShow = 2,

    /// <summary>An admin issued goodwill credit outside any dispute.</summary>
    Goodwill = 3,

    /// <summary>Spent against an order at checkout.</summary>
    OrderPayment = 10,

    /// <summary>Returned to the balance because the order it paid for was refunded or cancelled.</summary>
    OrderPaymentReturned = 11,

    /// <summary>
    /// Taken because it expired, or because an admin discharged it at the customer's request.
    ///
    /// <para>One reason for both: the movement and its consequence are identical, and the note on the
    /// row says which it was. An admin discharging a balance is how a customer who wants to be erased
    /// gets past the positive-balance refusal. → CreditAccount.ExpiryMonths</para>
    /// </summary>
    Expired = 12,
}

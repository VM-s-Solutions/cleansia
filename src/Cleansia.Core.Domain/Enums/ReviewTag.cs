using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

/// <summary>
/// The closed set of "what went well / what went wrong" tags a customer may attach to an order review.
///
/// <para><b>Server-owned on purpose.</b> The sibling chip surface — cancellation reasons — is
/// client-owned: a private enum per app whose code is prefixed onto a free-text field, landing in
/// <c>Order.CancellationReason</c> as an unparsed string. That shape costs nothing to ship and produces
/// nothing anyone can query. Review tags follow <see cref="DisputeReason"/> instead, on the owner's
/// ruling, because the question "what are the top three complaints this month" is one this platform
/// should be able to answer without parsing prose.</para>
///
/// <para><b>Polarity is encoded in the numbering, not in a second column.</b> Positive tags occupy
/// 1–10 and negative tags 11–20, with room left in each band so a later insert never renumbers a
/// shipped value — an enum member's integer IS the wire contract, and three clients decode it.
/// <c>ReviewTagPolarity</c> is the single reader of that convention.</para>
///
/// <para><b>There is deliberately no "something was damaged" tag.</b> Damage is
/// <see cref="DisputeReason.DamagedProperty"/> and it sits on the money path (ADR-0006, ADR-0009). A tag
/// would give the customer the feeling of having reported it with none of the mechanism — no refund, no
/// claim, no evidence upload. The clients surface a dispute link on a low rating instead.</para>
/// </summary>
[SwaggerEnumAsInt]
public enum ReviewTag
{
    // ─── Positive (offered at 4–5 stars) ───
    OnTime = 1,
    Thorough = 2,
    Friendly = 3,
    CarefulWithBelongings = 4,
    ExtrasDoneWell = 5,
    FollowedInstructions = 6,
    GreatPhotos = 7,

    // ─── Negative (offered at 1–3 stars) ───
    ArrivedLate = 11,
    MissedAreas = 12,
    FeltRushed = 13,
    ExtraNotDone = 14,
    DidNotFollowInstructions = 15,
    Unprofessional = 16,
    SmellOrProducts = 17,
    CrewSmallerThanBooked = 18
}

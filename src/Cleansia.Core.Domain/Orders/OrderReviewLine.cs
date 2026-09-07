using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// How one item of the order was rated, when the customer wanted to say more than one number for the
/// whole job.
///
/// <para>The order-level <see cref="OrderReview.Rating"/> stays the headline and stays required —
/// this does not replace it, and a review with no lines is an ordinary review. What it adds is the
/// thing an order-level rating cannot express: the oven was excellent and the bathroom was skipped.
/// Without it, a three-star review says a cleaner was mediocre when the truth was that they were
/// good at four things and missed one.</para>
///
/// <para>Same <c>(ServiceId, PackageId?)</c> identity as <see cref="Disputes.DisputeLine"/> and
/// <c>RefundLineSelection</c>, so the three name an order's items identically.</para>
/// </summary>
public class OrderReviewLine : Auditable
{
    [Required]
    [MaxLength(26)]
    public string OrderReviewId { get; private set; } = default!;
    public OrderReview? Review { get; private set; }

    [Required]
    [MaxLength(26)]
    public string ServiceId { get; private set; } = default!;

    /// <summary>Null for a standalone service; set when the service came inside a package.</summary>
    [MaxLength(26)]
    public string? PackageId { get; private set; }

    /// <summary>
    /// 1–5, the same scale as the order-level rating so the two are directly comparable and a client
    /// can render one control for both.
    /// </summary>
    [Required]
    [Range(1, 5)]
    public int Rating { get; private set; }

    private OrderReviewLine() { }

    public static OrderReviewLine Create(
        string orderReviewId, string serviceId, string? packageId, int rating, string createdBy)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rating, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rating, 5);

        var line = new OrderReviewLine
        {
            OrderReviewId = orderReviewId,
            ServiceId = serviceId,
            PackageId = packageId,
            Rating = rating,
        };
        line.Created(createdBy, DateTimeOffset.UtcNow);
        return line;
    }

    /// <summary>
    /// Erasure keeps the score and drops nothing else — there is nothing else here. A rating against
    /// a service carries no identifier and no free text, and the cleaner's history is not the
    /// subject's to delete. Same reasoning <see cref="OrderReview.Anonymize"/> already applies to the
    /// order-level rating and its tags.
    /// </summary>
    public static bool SurvivesErasure => true;
}

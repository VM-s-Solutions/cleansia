using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Orders;

public class OrderReview : Auditable, ITenantEntity
{
    [Required]
    [MaxLength(26)]
    public string OrderId { get; private set; }
    public Order? Order { get; private set; }

    [Required]
    [MaxLength(26)]
    public string UserId { get; private set; }

    [Required]
    [Range(1, 5)]
    public int Rating { get; private set; }

    [MaxLength(1000)]
    public string? Comment { get; private set; }

    /// <summary>
    /// The chips the customer picked, as a stored jsonb array of <see cref="ReviewTag"/> values.
    ///
    /// <para>A column rather than a child table: no caller needs to join on a tag today, and one column
    /// is the smaller thing to carry. jsonb rather than a converted text blob because the whole reason
    /// this is a server-owned enum is that someone will eventually ask which complaint is most common —
    /// <c>"Tags" @&gt; '[12]'</c> answers that, and takes a GIN index when the volume earns one.</para>
    ///
    /// <para>Never null: an empty list is a review with no chips, which is a perfectly ordinary review.
    /// Callers get a distinguishable "none" without a null check.</para>
    /// </summary>
    public IReadOnlyList<ReviewTag> Tags { get; private set; } = [];

    public static OrderReview Create(
        string orderId,
        string userId,
        int rating,
        string? comment,
        IReadOnlyList<ReviewTag>? tags = null) => new()
    {
        OrderId = orderId,
        UserId = userId,
        Rating = rating,
        Comment = comment,
        Tags = Normalize(tags)
    };

    public OrderReview Update(int rating, string? comment, IReadOnlyList<ReviewTag>? tags = null)
    {
        Rating = rating;
        Comment = comment;
        Tags = Normalize(tags);
        return this;
    }

    // Deduplicated and ordered so two reviews carrying the same chips compare and read identically,
    // whatever order the client's chip row happened to be tapped in.
    private static IReadOnlyList<ReviewTag> Normalize(IReadOnlyList<ReviewTag>? tags) =>
        tags is null ? [] : [.. tags.Distinct().OrderBy(tag => (int)tag)];

    public OrderReview Anonymize()
    {
        var suffix = Id.Length > 16 ? Id[..16] : Id;
        UserId = $"[DEL]_{suffix}";
        Comment = null;
        // Tags survive erasure deliberately. They are a closed set of platform-authored codes with no
        // free text and no identifier in them, so they carry nothing about the subject — and the
        // cleaner's rating history, which the codes describe, is not the subject's to delete. Same
        // reasoning as the Rating on the line above, which erasure has always left alone.
        return this;
    }
}

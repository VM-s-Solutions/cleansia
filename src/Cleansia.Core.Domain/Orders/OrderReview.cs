using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

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

    public static OrderReview Create(string orderId, string userId, int rating, string? comment) => new()
    {
        OrderId = orderId,
        UserId = userId,
        Rating = rating,
        Comment = comment
    };

    public OrderReview Update(int rating, string? comment)
    {
        Rating = rating;
        Comment = comment;
        return this;
    }

    public OrderReview Anonymize()
    {
        var suffix = Id.Length > 16 ? Id[..16] : Id;
        UserId = $"[DEL]_{suffix}";
        Comment = null;
        return this;
    }
}

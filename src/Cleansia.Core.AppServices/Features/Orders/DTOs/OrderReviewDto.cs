using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Orders.DTOs;

public record OrderReviewDto(
    string Id,
    string OrderId,
    int Rating,
    string? Comment,
    IReadOnlyList<ReviewTag> Tags,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn,
    /// <summary>
    /// Per-item scores, when the customer gave them. Empty is ordinary — the order-level
    /// <see cref="Rating"/> is the headline and is what every review carried before this existed.
    /// </summary>
    IReadOnlyList<OrderReviewLineDto> Lines);

/// <summary>One item of the order and what the customer thought of it.</summary>
public record OrderReviewLineDto(
    string ServiceId,
    /// <summary>Null when the service was bought on its own rather than inside a package.</summary>
    string? PackageId,
    int Rating);

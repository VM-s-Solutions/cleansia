using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Orders.DTOs;

public record OrderReviewDto(
    string Id,
    string OrderId,
    int Rating,
    string? Comment,
    IReadOnlyList<ReviewTag> Tags,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn);

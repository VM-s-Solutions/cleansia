namespace Cleansia.Core.AppServices.Features.Orders.DTOs;

public record OrderReviewDto(
    string Id,
    string OrderId,
    string UserId,
    int Rating,
    string? Comment,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn);

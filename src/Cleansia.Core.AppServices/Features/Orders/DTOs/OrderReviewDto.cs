namespace Cleansia.Core.AppServices.Features.Orders.DTOs;

public record OrderReviewDto(
    string Id,
    string OrderId,
    int Rating,
    string? Comment,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn);

using Cleansia.Core.AppServices.Shared.DTOs.Enums;

namespace Cleansia.Core.AppServices.Features.Disputes.DTOs;

public record DisputeListItem(
    string Id,
    string OrderId,
    string DisplayOrderNumber,
    string CustomerName,
    string CustomerEmail,
    Code Reason,
    Code Status,
    DateTimeOffset CreatedOn,
    DateTimeOffset? ResolvedOn,
    decimal? RefundAmount
);

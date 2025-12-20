using Cleansia.Core.AppServices.Shared.DTOs.Enums;

namespace Cleansia.Core.AppServices.Features.Disputes.DTOs;

public record DisputeDetails(
    string Id,
    string OrderId,
    string DisplayOrderNumber,
    string UserId,
    string CustomerName,
    string CustomerEmail,
    Code Reason,
    string Description,
    Code Status,
    string? ResolutionNotes,
    decimal? RefundAmount,
    string? ResolvedBy,
    DateTimeOffset? ResolvedOn,
    string? StripeDisputeId,
    IEnumerable<DisputeMessageDto> Messages,
    IEnumerable<DisputeEvidenceDto> Evidence,
    DateTimeOffset CreatedOn,
    string CreatedBy,
    DateTimeOffset? UpdatedOn,
    string? UpdatedBy
);

using Cleansia.Core.AppServices.Shared.DTOs.Enums;

namespace Cleansia.Core.AppServices.Features.Disputes.DTOs;

public record DisputeDetails(
    string Id,
    string OrderId,
    string DisplayOrderNumber,
    string CustomerName,
    string CustomerEmail,
    Code Reason,
    string Description,
    Code Status,
    string? ResolutionNotes,
    decimal? RefundAmount,
    DateTimeOffset? ResolvedOn,
    IEnumerable<DisputeMessageDto> Messages,
    IEnumerable<DisputeEvidenceDto> Evidence,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn
);

using Cleansia.Core.AppServices.Features.Currencies.DTOs;
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
    /// <summary>
    /// The currency the agreed refund is in — the ORDER's, since that is what is being refunded.
    /// Null only when the dispute's order could not be loaded.
    ///
    /// <para>Owner, 2026-09-03: the customer screen was formatting the refund as CZK unconditionally,
    /// because nothing on this DTO said otherwise. That is right while CZ is the only market and
    /// wrong on the first day it is not.</para>
    /// </summary>
    CurrencyDetailDto? Currency,
    DateTimeOffset? ResolvedOn,
    IEnumerable<DisputeMessageDto> Messages,
    IEnumerable<DisputeEvidenceDto> Evidence,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn
);

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
    DateTimeOffset? UpdatedOn,
    /// <summary>
    /// Whether the customer reported this inside the advertised window
    /// (<see cref="Cleansia.Core.Domain.Disputes.DisputeLimits.FilingWindowHours"/>).
    ///
    /// <para>The window gates the GUARANTEE, not the door: a late dispute is accepted and judged on
    /// its merits — a serious case has to be investigable — so this is the flag that lets an admin
    /// tell "we promised to fix this" from "we are choosing to". Without it the 24 hours would be a
    /// number in the copy that nothing in the platform can act on.</para>
    ///
    /// <para>Null when the dispute's order could not be loaded, which is the same condition that
    /// leaves <see cref="Currency"/> null.</para>
    /// </summary>
    bool? FiledWithinWindow,
    /// <summary>
    /// The order items the customer said were not done properly. Empty is ordinary — a dispute about
    /// the whole job, or about a charge, names none.
    /// </summary>
    IEnumerable<DisputeLineDto> Lines
);

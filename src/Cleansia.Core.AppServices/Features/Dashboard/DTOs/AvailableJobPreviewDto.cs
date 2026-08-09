namespace Cleansia.Core.AppServices.Features.Dashboard.DTOs;

/// <summary>
/// Slim order row for the mobile dashboard's "available jobs" hero card.
/// Just enough fields to render the row and compute "earn up to €X" without
/// pulling the full paged list. Sorted by totalPrice DESC server-side so the
/// highest-value job is first.
/// </summary>
public record AvailableJobPreviewDto(
    string Id,
    string? DisplayOrderNumber,
    /// <summary>
    /// City + zip-prefix, the same value and the same builder as
    /// <c>OrderListItem.CustomerAddressApproximate</c>. Every row on this surface is by construction
    /// an order the caller is NOT assigned to, so there is no branch on which a street belongs here.
    /// </summary>
    string CustomerAddressApproximate,
    DateTime? CleaningDateTime,
    decimal TotalPrice);

public record AvailableJobsPreviewResponse(
    IReadOnlyList<AvailableJobPreviewDto> Jobs,
    decimal TotalPotentialEarnings,
    int TotalAvailableCount);

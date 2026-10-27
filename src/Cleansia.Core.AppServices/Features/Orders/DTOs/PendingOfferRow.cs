namespace Cleansia.Core.AppServices.Features.Orders.DTOs;

/// <summary>
/// Server-side projection shape for the pending-offers query — exactly the columns
/// <see cref="PendingOfferItem"/> needs, plus the ones its pay estimate reads. Separate from the wire DTO
/// because the approximate address and the estimate are built in memory, the same split
/// <see cref="OrderListRow"/> uses. Never leaves the backend.
/// </summary>
public sealed record PendingOfferRow(
    string Id,
    string DisplayOrderNumber,
    DateTime CleaningDateTime,
    int EstimatedTime,
    DateTime RespondByUtc,
    string? City,
    string? ZipCode,
    int Rooms,
    int Bathrooms,
    decimal TotalPrice,
    string CurrencyCode,
    string CurrencyId,
    IReadOnlyCollection<string> ServiceIds,
    IReadOnlyCollection<string> PackageIds,
    decimal ExtrasSubtotal,
    int RequiredEmployees,
    decimal DirtinessRate);

namespace Cleansia.Core.AppServices.Features.Orders.DTOs;

/// <summary>
/// Server-side projection shape for the pending-offers query — exactly the columns
/// <see cref="PendingOfferItem"/> needs. Separate from the wire DTO because the approximate address is
/// built in memory (its zip truncation does not translate to SQL), the same split
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
    string CurrencyCode);

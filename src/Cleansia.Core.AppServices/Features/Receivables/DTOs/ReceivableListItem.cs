using Cleansia.Core.AppServices.Shared.DTOs.Enums;

namespace Cleansia.Core.AppServices.Features.Receivables.DTOs;

public record ReceivableListItem(
    string Id,
    string OrderId,
    string DisplayOrderNumber,
    string UserId,
    Code Kind,
    Code Status,
    decimal Amount,
    string CurrencyCode,
    int Attempts,
    DateTimeOffset CreatedOn,
    DateTimeOffset? WrittenOffOn,
    string? WriteOffNote);

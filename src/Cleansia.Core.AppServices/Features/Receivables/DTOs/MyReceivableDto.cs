using Cleansia.Core.AppServices.Shared.DTOs.Enums;

namespace Cleansia.Core.AppServices.Features.Receivables.DTOs;

/// <summary>What the customer owes on one of their orders and has not settled.</summary>
public record MyReceivableDto(
    string Id,
    string OrderId,
    string DisplayOrderNumber,
    Code Kind,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset CreatedOn);

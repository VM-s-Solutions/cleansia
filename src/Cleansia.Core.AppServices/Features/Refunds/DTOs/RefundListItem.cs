using Cleansia.Core.AppServices.Shared.DTOs.Enums;

namespace Cleansia.Core.AppServices.Features.Refunds.DTOs;

public record RefundListItem(
    string Id,
    string OrderId,
    string DisplayOrderNumber,
    string? DisputeId,
    string? UserId,
    string CustomerName,
    bool HasAccount,
    decimal Amount,
    string CurrencyCode,
    Code Origin,
    Code State,
    DateTimeOffset RequestedOn,
    DateTimeOffset LastChangedOn,
    DateTimeOffset? RepaidOn,
    Code? RepaymentMethod,
    string? RepaymentReference,
    string? StripeRefundId,
    bool CanRetry);

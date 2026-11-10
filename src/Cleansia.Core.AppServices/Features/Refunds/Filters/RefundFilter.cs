using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Refunds.Filters;

public record RefundFilter(
    RefundState[]? States,
    string? UserId,
    string? OrderId,
    DateTimeOffset? RequestedFrom,
    DateTimeOffset? RequestedTo);

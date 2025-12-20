#nullable enable
namespace Cleansia.Core.AppServices.Features.Disputes.Filters;

public record DisputeFilter(
    string? OrderId,
    string? UserId,
    string? CustomerName,
    string? CustomerEmail,
    int[]? Statuses,
    int[]? Reasons,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedTo,
    DateTimeOffset? ResolvedFrom,
    DateTimeOffset? ResolvedTo,
    decimal? MinRefundAmount,
    decimal? MaxRefundAmount);

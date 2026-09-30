using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Receivables.Filters;

public record ReceivableFilter(
    ReceivableStatus? Status,
    ReceivableKind? Kind,
    string? UserId,
    string? OrderId);

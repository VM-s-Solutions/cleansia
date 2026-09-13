#nullable enable
namespace Cleansia.Core.AppServices.Features.Auditing.Filters;

public record CustomerActionAuditFilter(
    string? UserId,
    string? Action,
    string? ResourceType,
    string? ResourceId,
    DateTimeOffset? OccurredFrom,
    DateTimeOffset? OccurredTo,
    bool? Success,
    string? ErrorCode,
    string? ClientAudience);

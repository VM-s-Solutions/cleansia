namespace Cleansia.Core.AppServices.Features.Auditing.DTOs;

public record CustomerActionAuditDto(
    string Id,
    string? UserId,
    string ClientAudience,
    string Action,
    string? ResourceType,
    string? ResourceId,
    bool Success,
    string? ErrorCode,
    DateTimeOffset OccurredOn);

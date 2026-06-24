using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Auditing.DTOs;

public record AdminActionAuditDetailDto(
    string Id,
    string ActorId,
    string? ActorEmail,
    UserProfile ActorProfile,
    string Action,
    string? ResourceType,
    string? ResourceId,
    bool Success,
    string? ErrorCode,
    DateTimeOffset OccurredOn,
    string? Reason,
    string? CorrelationId,
    string? BeforeJson,
    string? AfterJson);

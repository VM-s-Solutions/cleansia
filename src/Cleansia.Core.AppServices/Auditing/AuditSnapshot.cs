using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Auditing;

public sealed record AuditSnapshot(
    string? ResourceType,
    string? ResourceId,
    string? BeforeJson,
    string? AfterJson,
    string? Reason,
    string? ActorUserId = null,
    UserProfile? ActorProfile = null);

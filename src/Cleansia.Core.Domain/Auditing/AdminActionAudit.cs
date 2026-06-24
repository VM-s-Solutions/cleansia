using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Auditing;

public sealed class AdminActionAudit : BaseEntity, ITenantEntity
{
    public string? TenantId { get; set; }

    public string ActorId { get; init; } = default!;

    public string? ActorEmail { get; init; }

    public UserProfile ActorProfile { get; init; }

    public string Action { get; init; } = default!;

    public string? ResourceType { get; init; }

    public string? ResourceId { get; init; }

    public bool Success { get; init; }

    public string? ErrorCode { get; init; }

    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;

    public string? Reason { get; init; }

    public string? BeforeJson { get; init; }

    public string? AfterJson { get; init; }

    public string? CorrelationId { get; init; }
}

using Cleansia.Core.AppServices.Shared.DTOs.Enums;

namespace Cleansia.Core.AppServices.Features.Auditing.DTOs;

/// <summary>
/// One row of the three-source timeline (ADR-0062 D6). <see cref="ActorId"/> is the source table's own
/// actor column: the customer's user id, the admin's user id, or the cleaner's employee id.
/// </summary>
public record TimelineEntryDto(
    TimelineSource Source,
    string Id,
    DateTimeOffset OccurredOn,
    string? ActorId,
    string Action,
    string? ResourceType,
    string? ResourceId,
    bool Success,
    string? ErrorCode);

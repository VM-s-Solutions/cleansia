#nullable enable
namespace Cleansia.Core.AppServices.Features.Auditing.Filters;

public record AdminActionAuditFilter(
    string? ActorId,
    string? ActorEmail,
    string? Action,
    string? ResourceType,
    string? ResourceId,
    DateTimeOffset? OccurredFrom,
    DateTimeOffset? OccurredTo,
    bool? Success);

using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Gdpr.DTOs;

public record GdprRequestDto(
    string Id,
    string UserId,
    string RequestType,
    GdprRequestStatus Status,
    string? ProcessedBy,
    DateTimeOffset? CompletedAt,
    string? Notes,
    DateTimeOffset CreatedOn
);

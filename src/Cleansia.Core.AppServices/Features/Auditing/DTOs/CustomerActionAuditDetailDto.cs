namespace Cleansia.Core.AppServices.Features.Auditing.DTOs;

/// <summary>
/// The single-row read is the only one that carries the payload and the three request-metadata
/// columns; the paged list withholds them (ADR-0062 D6, the ADR-0012 D4.1 projection discipline).
/// <see cref="CurrencyCode"/> is resolved by the reader from a top-level <c>currencyId</c> in the
/// payload — the writer records the id it already holds rather than paying an extra include.
/// </summary>
public record CustomerActionAuditDetailDto(
    string Id,
    string? UserId,
    string ClientAudience,
    string? IpAddress,
    string? DeviceLabel,
    string? DeviceId,
    string Action,
    string? ResourceType,
    string? ResourceId,
    bool Success,
    string? ErrorCode,
    DateTimeOffset OccurredOn,
    string? PayloadJson,
    string? CorrelationId,
    string? CurrencyCode);

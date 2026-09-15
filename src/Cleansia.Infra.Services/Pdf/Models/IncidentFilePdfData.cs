namespace Cleansia.Infra.Services.Pdf.Models;

/// <summary>
/// Everything the incident file prints, already resolved: currency ids are codes, enums are names,
/// audit payloads are flat key/value fields. The renderer formats and lays out; it never looks anything
/// up. This is the one document on the platform that prints a customer's identity on purpose — it is
/// built for a lawyer, behind <c>CanAdminExportUserData</c>, and every build is an audited admin act.
/// </summary>
public sealed record IncidentFilePdfData(
    IncidentFileSubject Subject,
    string? OrderIdFilter,
    IReadOnlyList<IncidentFileOrder> Orders,
    IReadOnlyList<IncidentFileDispute> Disputes,
    IReadOnlyList<IncidentFileConsent> Consents,
    IReadOnlyList<IncidentFileTrailEntry> Trail,
    bool TrailTruncated,
    DateTimeOffset GeneratedAt,
    string GeneratedBy);

/// <summary>
/// <paramref name="OperatorName"/> is the operating company's display name and <paramref name="Market"/>
/// the countries it serves, both resolved from the market registry — never the tenant id the user row
/// carries, which is an internal key and not a fact about the subject (S4). Null when the registry names
/// no market for the operator; the page prints a blank, not the key.
/// </summary>
public sealed record IncidentFileSubject(
    string UserId,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    DateTimeOffset AccountCreatedOn,
    string? OperatorName,
    string? Market,
    string? PreferredLanguage,
    bool Erased,
    DateTimeOffset? ErasedOn);

public sealed record IncidentFileOrder(
    string Id,
    string Number,
    DateTimeOffset CreatedOn,
    DateTime CleaningDateTime,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    string Address,
    IReadOnlyList<IncidentFileOrderLine> Lines,
    decimal TotalPrice,
    string Currency,
    string PaymentType,
    string PaymentStatus,
    string Status,
    IReadOnlyList<IncidentFileStatusChange> StatusHistory,
    IReadOnlyList<IncidentFileRefund> Refunds,
    IReadOnlyList<IncidentFileCleaner> AssignedCleaners,
    string? CancelledBy,
    string? CancellationReason);

public sealed record IncidentFileOrderLine(string Kind, string Name, decimal Amount);

public sealed record IncidentFileStatusChange(string Status, DateTimeOffset At);

public sealed record IncidentFileRefund(
    decimal Amount,
    string Currency,
    string Reason,
    string Source,
    string Status,
    DateTimeOffset CreatedOn,
    DateTimeOffset? ConfirmedOn);

public sealed record IncidentFileCleaner(string EmployeeId, string FirstName);

public sealed record IncidentFileDispute(
    string Id,
    string OrderNumber,
    string Reason,
    string Status,
    string Description,
    DateTimeOffset CreatedOn,
    IReadOnlyList<IncidentFileDisputeMessage> Messages,
    IReadOnlyList<string> EvidenceFileNames,
    string? ResolutionNotes,
    decimal? RefundAmount,
    string? ResolvedBy,
    DateTimeOffset? ResolvedOn);

public sealed record IncidentFileDisputeMessage(string AuthorRole, string AuthorId, DateTimeOffset At, string Text);

public sealed record IncidentFileConsent(
    string Type,
    string? DocumentVersion,
    DateOnly? EffectiveFrom,
    bool IsGranted,
    DateTimeOffset? GrantedAt,
    DateTimeOffset? WithdrawnAt,
    string? IpAddress,
    string? UserAgent);

/// <summary>
/// One row of any of the three audit tables. <paramref name="Source"/> names the table (Customer,
/// Admin, Cleaner); <paramref name="Evidence"/> is the row's payload flattened to key/value pairs, in
/// the order the payload declared them.
/// </summary>
public sealed record IncidentFileTrailEntry(
    string Source,
    DateTimeOffset OccurredOn,
    string ActorRole,
    string? ActorId,
    string Action,
    string? ResourceType,
    string? ResourceId,
    bool Success,
    string? ErrorCode,
    IReadOnlyList<IncidentFileEvidenceField> Evidence,
    string? IpAddress,
    string? DeviceLabel);

public sealed record IncidentFileEvidenceField(string Key, string Value);

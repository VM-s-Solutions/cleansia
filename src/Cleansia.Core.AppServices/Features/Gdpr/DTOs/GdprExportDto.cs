using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Gdpr.DTOs;

public record GdprExportDto(
    GdprExportProfileDto Profile,
    GdprExportAddressDto? Address,
    GdprExportEmployeeDto? Employee,
    GdprExportPayoutDetailsDto? PayoutDetails,
    List<GdprExportOrderDto> Orders,
    List<GdprExportDisputeDto> Disputes,
    List<GdprExportDocumentDto> Documents,
    List<GdprExportInvoiceDto> Invoices,
    List<GdprExportConsentDto> Consents,
    List<GdprExportCustomerActionDto> CustomerActions,
    GdprExportMetadataDto Metadata
);

public record GdprExportProfileDto(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    DateOnly? BirthDate,
    string? PreferredLanguageCode,
    DateTimeOffset CreatedOn
);

public record GdprExportAddressDto(
    string Street,
    string City,
    string ZipCode,
    string? State,
    string? CountryId
);

public record GdprExportEmployeeDto(
    string Id,
    EmployeeEntityType EntityType,
    string? RegistrationNumber,
    string? LegalEntityName,
    string? IBAN,
    string? PassportId,
    string? NationalityId,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? PreferredCurrencyCode,
    decimal AverageRating,
    ContractStatus ContractStatus,
    DateTimeOffset CreatedOn
);

/// <summary>
/// ADR-0034 — the payout destination is the subject's own data and a subject-access export that silently
/// dropped it would be a compliance regression introduced by a feature. This DTO and the payout read
/// contract are the only places a payout identifier is allowed to appear.
/// </summary>
public record GdprExportPayoutDetailsDto(
    PayoutScheme? Scheme,
    PayoutDetailsStatus Status,
    string? BankCountryId,
    string? CurrencyId,
    string? AccountPrefix,
    string? AccountNumber,
    string? BankCode,
    string? Iban,
    string? Swift,
    string? BankName,
    string? HolderName,
    DateTime? ConfirmedAt,
    DateTime? LastRevealedAt,
    int RevealCount
);

/// <summary>
/// One row per order that is the subject's under <c>SubjectOrders</c> — the orders booked on the account
/// AND the guest bookings placed with the account's e-mail address (owner ruling 2026-09-15), in any
/// market. The orders section and the erasure walk read the same set, so an order the erasure would
/// anonymise is an order this lists (a guest booking still live is listed here and left for the sweep
/// there); a guest booking under another address is never listed, whoever asks. The trail section is
/// narrower: the account's own rows only, never the guest rows the erasure blanks on these orders.
/// </summary>
public record GdprExportOrderDto(
    string Id,
    string DisplayOrderNumber,
    string CustomerName,
    string CustomerEmail,
    OrderStatus Status,
    decimal TotalPrice,
    DateTime CleaningDateTime,
    DateTimeOffset CreatedOn
);

/// <summary>
/// One row per dispute that is the subject's: filed on the account, or on an order of
/// <c>SubjectOrders</c> — so the section follows the orders section, and an erased subject, whose
/// orders no longer name the account, still gets the disputes the account filed. Reason and status are
/// the enum NAMES, not the wire integers the other sections carry: this document is read by the
/// subject, not by a client. The text is exported as stored — the description, the messages and the
/// resolution notes until the retention window closes and the marker after the sweep, the evidence
/// names as the marker from the erasure on. A message carries its author's role and never the staff
/// member's id.
/// </summary>
public record GdprExportDisputeDto(
    string Id,
    string OrderId,
    string OrderDisplayNumber,
    string Reason,
    string Description,
    string Status,
    string? ResolutionNotes,
    decimal? RefundAmount,
    string CurrencyCode,
    DateTimeOffset CreatedOn,
    DateTimeOffset? ResolvedOn,
    List<GdprExportDisputeMessageDto> Messages,
    List<string> EvidenceFileNames
);

public record GdprExportDisputeMessageDto(
    string AuthorRole,
    DateTimeOffset SentAt,
    string Text
);

public record GdprExportDocumentDto(
    string Id,
    string FileName,
    string? DocumentType,
    DateTimeOffset CreatedOn
);

public record GdprExportInvoiceDto(
    string Id,
    string InvoiceNumber,
    decimal TotalAmount,
    EmployeeInvoiceStatus Status,
    DateTimeOffset CreatedOn
);

public record GdprExportConsentDto(
    string Id,
    ConsentType ConsentType,
    bool IsGranted,
    DateTimeOffset? GrantedAt,
    DateTimeOffset? WithdrawnAt,
    string? IpAddress,
    string? UserAgent,
    string? DocumentVersion,
    string? LegalDocumentId
);

/// <summary>
/// ADR-0062 D5 — the subject's own conduct record, row for row as the platform holds it: the act, its
/// outcome, the evidence payload and the request context. After an erasure the IP address and device
/// label read null because the row was pseudonymised, and the payload is still there because it is
/// what a dispute is answered from.
/// </summary>
public record GdprExportCustomerActionDto(
    string Action,
    DateTimeOffset OccurredOn,
    string? ResourceType,
    string? ResourceId,
    bool Success,
    string? ErrorCode,
    string? PayloadJson,
    string? IpAddress,
    string? DeviceLabel
);

public record GdprExportMetadataDto(
    DateTimeOffset ExportedAt,
    string ExportedBy,
    string DataFormat
);

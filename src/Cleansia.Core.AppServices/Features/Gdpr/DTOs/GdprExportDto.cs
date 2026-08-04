using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Gdpr.DTOs;

public record GdprExportDto(
    GdprExportProfileDto Profile,
    GdprExportAddressDto? Address,
    GdprExportEmployeeDto? Employee,
    GdprExportPayoutDetailsDto? PayoutDetails,
    List<GdprExportOrderDto> Orders,
    List<GdprExportDocumentDto> Documents,
    List<GdprExportInvoiceDto> Invoices,
    List<GdprExportConsentDto> Consents,
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
    string? VatNumber,
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
    DateTimeOffset? WithdrawnAt
);

public record GdprExportMetadataDto(
    DateTimeOffset ExportedAt,
    string ExportedBy,
    string DataFormat
);

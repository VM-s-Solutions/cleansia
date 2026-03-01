using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Gdpr.DTOs;

public record GdprExportDto(
    GdprExportProfileDto Profile,
    GdprExportAddressDto? Address,
    GdprExportEmployeeDto? Employee,
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
    string? TaxId,
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

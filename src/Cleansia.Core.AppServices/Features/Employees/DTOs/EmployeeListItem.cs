using Cleansia.Core.AppServices.Shared.DTOs.Files;

namespace Cleansia.Core.AppServices.Features.Employees.DTOs;

public record EmployeeListItem(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string Profile,
    string AuthenticationType,
    DateOnly? BirthDate,
    BlobFileDto? ProfilePhoto
);

public record AdminEmployeeListItem(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string ContractStatus,
    decimal AverageRating,
    int ComplaintsCount,
    string? NationalityName,
    DateTimeOffset CreatedAt,
    bool IsProfileComplete
);

public record AdminEmployeeDetail(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    DateOnly? BirthDate,
    string? Street,
    string? City,
    string? ZipCode,
    string? CountryId,
    string? State,
    string? CountryName,
    string? NationalityId,
    string? NationalityName,
    string? PassportId,
    string? TaxId,
    string? Iban,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string ContractStatus,
    decimal AverageRating,
    int ComplaintsCount,
    Dictionary<string, List<TimeRange>>? Availability,
    DateTimeOffset CreatedAt,
    bool IsProfileComplete,
    string? RejectionReason,
    string? ApprovalNotes,
    string? ApprovedByUserId,
    DateTimeOffset? ApprovedAt,
    string? RejectedByUserId,
    DateTimeOffset? RejectedAt,
    List<string> MissingFields
);

public record TimeRange(string Start, string End);

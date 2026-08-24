using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Domain.Enums;

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
    EmployeeEntityType EntityType,
    string? RegistrationNumber,
    string? VatNumber,
    string? LegalEntityName,
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
    List<string> MissingFields,
    string UserId,
    /// <summary>
    /// The cleaner's per-week order cap, or null for unlimited (the default).
    ///
    /// <para><b>The read surface ADR-0053 left open.</b> The cap was settable by one admin against one
    /// cleaner and readable by nobody — so a second admin could not see it existed, and the cleaner's
    /// only feedback was a refusal at the moment they tried to take work.</para>
    /// </summary>
    int? WeeklyOrderLimit
);

public record TimeRange(string Start, string End);

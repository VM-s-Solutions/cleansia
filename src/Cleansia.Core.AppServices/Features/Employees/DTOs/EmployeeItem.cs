using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Employees.DTOs;

public record EmployeeItem(
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
    string? NationalityId,
    string? PassportId,
    EmployeeEntityType EntityType,
    string? RegistrationNumber,
    string? LegalEntityName,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    // Ahead of the photo on purpose: its base64 is redacted in the request log, and text behind it would
    // be pulled into the logged window.
    int? WeeklyOrderLimit,
    string? WeeklyOrderLimitReason,
    BlobFileDto? ProfilePhoto,
    Code Profile,
    Code AuthenticationType,
    int? JobRadiusKm);
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Domain.Users;
using static Cleansia.Core.AppServices.Features.Employees.UpdateEmployee;

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
    string? NationalityId,
    string? PassportId,
    string? TaxId,
    string? Iban,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    BlobFileDto? ProfilePhoto,
    Code Profile,
    Code AuthenticationType,
    Dictionary<string, List<TimeRange>>? Availability);
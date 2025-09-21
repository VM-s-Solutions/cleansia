using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.AppServices.Shared.DTOs.Files;

namespace Cleansia.Core.AppServices.Features.Employees.DTOs;

public record EmployeeListItem(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    Code Profile,
    Code AuthenticationType,
    DateOnly? BirthDate,
    BlobFileDto? ProfilePhoto);
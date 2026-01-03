using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.Domain.Users;
using DtoTimeRange = Cleansia.Core.AppServices.Features.Employees.DTOs.TimeRange;

namespace Cleansia.Core.AppServices.Mappers;

public static class EmployeeMappers
{
    public static RegistrationCompletionStatus ToRegistrationCompletionStatus(this Employee employee)
    {
        return new RegistrationCompletionStatus(
            AreDocumentsUploaded: employee.ContractStatus != Domain.Enums.ContractStatus.Pending,
            HasCompletedProfile: !string.IsNullOrWhiteSpace(employee.ICO)
                                 && employee.Address is not null
                                 && employee.Availability.Any());
    }

    public static EmployeeListItem MapToDto(this Employee employee)
    {
        return new EmployeeListItem(
            Id: employee.Id,
            Email: employee.User!.Email,
            FirstName: employee.User.FirstName,
            LastName: employee.User.LastName,
            PhoneNumber: employee.User.PhoneNumber,
            Profile: employee.User.Profile.MapToCode().Name,
            AuthenticationType: employee.User.AuthenticationType.MapToCode().Name,
            BirthDate: employee.User.BirthDate,
            ProfilePhoto: employee.User.ProfilePhotoName?.MapToDto()
        );
    }

    public static EmployeeItem MapToEmployeeItem(this Employee employee)
    {
        return new EmployeeItem(
            Id: employee.Id,
            Email: employee.User!.Email,
            FirstName: employee.User.FirstName,
            LastName: employee.User.LastName,
            PhoneNumber: employee.User.PhoneNumber,
            BirthDate: employee.User.BirthDate,
            Street: employee.Address?.Street,
            City: employee.Address?.City,
            ZipCode: employee.Address?.ZipCode,
            CountryId: employee.Address?.CountryId,
            NationalityId: employee.NationalityId,
            PassportId: employee.PassportId,
            TaxId: employee.ICO,
            Iban: employee.IBAN,
            EmergencyContactName: employee.EmergencyContactName,
            EmergencyContactPhone: employee.EmergencyContactPhone,
            ProfilePhoto: employee.User.ProfilePhotoName?.MapToDto(),
            Profile: employee.User.Profile.MapToCode(),
            AuthenticationType: employee.User.AuthenticationType.MapToCode(),
            Availability: employee.Availability?.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(tr => new DtoTimeRange(
                    TimeOnly.FromTimeSpan(tr.Start),
                    TimeOnly.FromTimeSpan(tr.End)
                )).ToList()
            ));
    }

    public static AdminEmployeeListItem MapToAdminDto(this Employee employee)
    {
        return new AdminEmployeeListItem(
            Id: employee.Id,
            FirstName: employee.User!.FirstName,
            LastName: employee.User.LastName,
            Email: employee.User.Email,
            PhoneNumber: employee.User.PhoneNumber,
            ContractStatus: employee.ContractStatus.ToString(),
            AverageRating: employee.AverageRating,
            ComplaintsCount: employee.ComplaintsCount,
            NationalityName: employee.Nationality?.Name,
            CreatedAt: employee.User.CreatedOn,
            IsProfileComplete: IsEmployeeProfileComplete(employee)
        );
    }

    public static AdminEmployeeDetail MapToAdminDetailDto(this Employee employee)
    {
        return new AdminEmployeeDetail(
            Id: employee.Id,
            Email: employee.User!.Email,
            FirstName: employee.User.FirstName,
            LastName: employee.User.LastName,
            PhoneNumber: employee.User.PhoneNumber,
            BirthDate: employee.User.BirthDate,
            Street: employee.Address?.Street,
            City: employee.Address?.City,
            ZipCode: employee.Address?.ZipCode,
            CountryId: employee.Address?.CountryId,
            CountryName: employee.Address?.Country?.Name,
            NationalityId: employee.NationalityId,
            NationalityName: employee.Nationality?.Name,
            PassportId: employee.PassportId,
            TaxId: employee.ICO,
            Iban: employee.IBAN,
            EmergencyContactName: employee.EmergencyContactName,
            EmergencyContactPhone: employee.EmergencyContactPhone,
            ContractStatus: employee.ContractStatus.ToString(),
            AverageRating: employee.AverageRating,
            ComplaintsCount: employee.ComplaintsCount,
            DocumentFileNames: employee.DocumentFileNames.ToList(),
            Availability: employee.Availability?.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(tr => new DtoTimeRange(
                    TimeOnly.FromTimeSpan(tr.Start),
                    TimeOnly.FromTimeSpan(tr.End)
                )).ToList()
            ),
            CreatedAt: employee.User.CreatedOn,
            IsProfileComplete: IsEmployeeProfileComplete(employee),
            RejectionReason: employee.RejectionReason,
            ApprovalNotes: employee.ApprovalNotes,
            ApprovedByUserId: employee.ApprovedByUserId,
            ApprovedAt: employee.ApprovedAt,
            RejectedByUserId: employee.RejectedByUserId,
            RejectedAt: employee.RejectedAt,
            MissingFields: employee.GetMissingProfileFields()
        );
    }

    private static bool IsEmployeeProfileComplete(Employee employee)
    {
        var hasBasicInfo = !string.IsNullOrEmpty(employee.User?.FirstName) &&
                           !string.IsNullOrEmpty(employee.User?.LastName) &&
                           !string.IsNullOrEmpty(employee.User?.Email) &&
                           !string.IsNullOrEmpty(employee.User?.PhoneNumber);

        var hasPersonalInfo = employee.User?.BirthDate.HasValue == true;

        var hasAddress = !string.IsNullOrEmpty(employee.Address?.Street) &&
                        !string.IsNullOrEmpty(employee.Address?.City) &&
                        !string.IsNullOrEmpty(employee.Address?.ZipCode) &&
                        !string.IsNullOrEmpty(employee.Address?.CountryId);

        var hasEmployeeInfo = !string.IsNullOrEmpty(employee.ICO) &&
                             !string.IsNullOrEmpty(employee.IBAN) &&
                             !string.IsNullOrEmpty(employee.PassportId) &&
                             !string.IsNullOrEmpty(employee.NationalityId);

        var hasEmergencyContact = !string.IsNullOrEmpty(employee.EmergencyContactName) &&
                                 !string.IsNullOrEmpty(employee.EmergencyContactPhone);

        var hasDocuments = employee.DocumentFileNames.Any();

        var hasAvailability = employee.Availability?.Any() == true;

        return hasBasicInfo && hasPersonalInfo && hasAddress &&
               hasEmployeeInfo && hasEmergencyContact && hasDocuments &&
               hasAvailability;
    }
}

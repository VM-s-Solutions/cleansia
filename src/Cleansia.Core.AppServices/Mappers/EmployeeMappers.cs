using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.Domain.Users;

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
            Profile: employee.User.Profile.MapToCode(),
            AuthenticationType: employee.User.AuthenticationType.MapToCode(),
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
            Availability: employee.Availability.ToDictionary());
    }
}
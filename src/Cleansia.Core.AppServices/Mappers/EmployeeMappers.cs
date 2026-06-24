using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.Domain.Users;
using DtoTimeRange = Cleansia.Core.AppServices.Features.Employees.DTOs.TimeRange;

namespace Cleansia.Core.AppServices.Mappers;

public static class EmployeeMappers
{
    public static RegistrationCompletionStatus ToRegistrationCompletionStatus(this Employee employee)
    {
        return new RegistrationCompletionStatus(
            AreDocumentsUploaded: employee.Documents.Any(d => d.IsActive),
            HasCompletedProfile: employee.IsProfileComplete(),
            // Availability is no longer part of the registration gate.
            // The field stays on the DTO for API-contract compatibility
            // (partner-web + the generated mobile client still expect it)
            // but is always true so it never blocks unlock. The weekly
            // schedule remains editable by admins and stored on the
            // Employee for potential future matching/push features.
            HasSetAvailability: true,
            MissingFields: employee.GetMissingProfileFields(),
            ContractStatus: employee.ContractStatus,
            RejectionReason: employee.RejectionReason);
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
            State: employee.Address?.State,
            NationalityId: employee.NationalityId,
            PassportId: employee.PassportId,
            EntityType: employee.EntityType,
            RegistrationNumber: employee.RegistrationNumber,
            VatNumber: employee.VatNumber,
            LegalEntityName: employee.LegalEntityName,
            Iban: employee.IBAN,
            EmergencyContactName: employee.EmergencyContactName,
            EmergencyContactPhone: employee.EmergencyContactPhone,
            ProfilePhoto: employee.User.ProfilePhotoName?.MapToDto(),
            Profile: employee.User.Profile.MapToCode(),
            AuthenticationType: employee.User.AuthenticationType.MapToCode(),
            Availability: employee.Availability?.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(tr => new DtoTimeRange(
                    tr.Start.ToString(@"hh\:mm"),
                    tr.End.ToString(@"hh\:mm")
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
            State: employee.Address?.State,
            CountryName: employee.Address?.Country?.Name,
            NationalityId: employee.NationalityId,
            NationalityName: employee.Nationality?.Name,
            PassportId: employee.PassportId,
            EntityType: employee.EntityType,
            RegistrationNumber: employee.RegistrationNumber,
            VatNumber: employee.VatNumber,
            LegalEntityName: employee.LegalEntityName,
            Iban: employee.IBAN,
            EmergencyContactName: employee.EmergencyContactName,
            EmergencyContactPhone: employee.EmergencyContactPhone,
            ContractStatus: employee.ContractStatus.ToString(),
            AverageRating: employee.AverageRating,
            ComplaintsCount: employee.ComplaintsCount,
            Availability: employee.Availability?.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(tr => new DtoTimeRange(
                    tr.Start.ToString(@"hh\:mm"),
                    tr.End.ToString(@"hh\:mm")
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
            MissingFields: employee.GetMissingProfileFields(),
            UserId: employee.UserId
        );
    }

    private static bool IsEmployeeProfileComplete(Employee employee)
    {
        return employee.IsProfileComplete();
    }
}

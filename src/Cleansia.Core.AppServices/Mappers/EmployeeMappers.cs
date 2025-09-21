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
}
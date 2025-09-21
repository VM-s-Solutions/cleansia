namespace Cleansia.Core.AppServices.Features.Employees.DTOs;

public record RegistrationCompletionStatus(
    bool AreDocumentsUploaded,
    bool HasCompletedProfile);
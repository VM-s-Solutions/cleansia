using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Employees.DTOs;

public record RegistrationCompletionStatus(
    bool AreDocumentsUploaded,
    bool HasCompletedProfile,
    bool HasSetAvailability,
    List<string> MissingFields,
    ContractStatus ContractStatus,
    string? RejectionReason);

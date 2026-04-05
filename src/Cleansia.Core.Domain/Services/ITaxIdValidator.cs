using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Services;

public interface ITaxIdValidator
{
    Task<TaxIdValidationResult> ValidateRegistrationNumberAsync(
        string countryId,
        EmployeeEntityType entityType,
        string? value,
        CancellationToken cancellationToken = default);

    Task<TaxIdValidationResult> ValidateVatNumberAsync(
        string countryId,
        string? value,
        CancellationToken cancellationToken = default);
}

public sealed record TaxIdValidationResult(bool IsValid, string? ErrorKey)
{
    public static TaxIdValidationResult Valid() => new(true, null);
    public static TaxIdValidationResult Invalid(string errorKey) => new(false, errorKey);
}

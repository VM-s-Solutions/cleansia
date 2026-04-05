using System.Text.RegularExpressions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Core.AppServices.Services;

public sealed class TaxIdValidator(ICountryConfigurationRepository countryConfigRepository) : ITaxIdValidator
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    public async Task<TaxIdValidationResult> ValidateRegistrationNumberAsync(
        string countryId,
        EmployeeEntityType entityType,
        string? value,
        CancellationToken cancellationToken = default)
    {
        _ = entityType;
        var config = await countryConfigRepository.GetByCountryIdAsync(countryId, cancellationToken);
        var required = config?.RegistrationNumberRequired ?? true;

        if (string.IsNullOrWhiteSpace(value))
        {
            return required
                ? TaxIdValidationResult.Invalid("validation.registration_number.required")
                : TaxIdValidationResult.Valid();
        }

        return MatchesFormat(value.Trim(), config?.RegistrationNumberFormat)
            ? TaxIdValidationResult.Valid()
            : TaxIdValidationResult.Invalid("validation.registration_number.invalid_format");
    }

    public async Task<TaxIdValidationResult> ValidateVatNumberAsync(
        string countryId,
        string? value,
        CancellationToken cancellationToken = default)
    {
        var config = await countryConfigRepository.GetByCountryIdAsync(countryId, cancellationToken);
        var required = config?.VatNumberRequired ?? false;

        if (string.IsNullOrWhiteSpace(value))
        {
            return required
                ? TaxIdValidationResult.Invalid("validation.vat_number.required")
                : TaxIdValidationResult.Valid();
        }

        return MatchesFormat(value.Trim(), config?.VatNumberFormat)
            ? TaxIdValidationResult.Valid()
            : TaxIdValidationResult.Invalid("validation.vat_number.invalid_format");
    }

    private static bool MatchesFormat(string value, string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return true;
        }

        try
        {
            return Regex.IsMatch(value, format, RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }
}

using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Payouts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Core.AppServices.Services;

/// <summary>
/// ADR-0034 D4. Scheme selection reads <c>CountryConfiguration.PayoutScheme</c> or the IBAN's own
/// country prefix — no handler and no branch here compares a country code literal (ADR-0017).
/// </summary>
public sealed class PayoutDetailsValidator(
    ICountryRepository countryRepository,
    ICountryConfigurationRepository countryConfigurationRepository) : IPayoutDetailsValidator
{
    public async Task<PayoutValidationResult> ValidateAsync(
        PayoutDetailsInput input,
        CancellationToken cancellationToken = default)
    {
        if (CardNumberHeuristic.LooksLikeCardNumber(input.AccountNumber) ||
            CardNumberHeuristic.LooksLikeCardNumber(input.Iban))
        {
            return PayoutValidationResult.Invalid(BusinessErrorMessage.PayoutLooksLikeCard);
        }

        var bankCountry = string.IsNullOrWhiteSpace(input.BankCountryId)
            ? null
            : await countryRepository.GetByIdAsync(input.BankCountryId, cancellationToken);

        var suppliedIban = IbanCalculator.Normalize(input.Iban);
        var suppliedIbanIsValid = IbanCalculator.IsValid(suppliedIban);

        // The bank country is the record's own field, but a mod-97-valid IBAN is self-describing by
        // construction, so a cleaner who supplies only an IBAN still gets a checkable answer.
        var bankCountryAlpha2 = CountryIsoCode.ToAlpha2(bankCountry?.IsoCode)
                                ?? (suppliedIbanIsValid ? IbanCalculator.CountryPrefixOf(suppliedIban) : null);

        if (bankCountryAlpha2 is null)
        {
            return PayoutValidationResult.Invalid(BusinessErrorMessage.PayoutCountryNotSupported);
        }

        var bankCountryId = bankCountry?.Id ?? (await ResolveCountryIdAsync(bankCountryAlpha2, cancellationToken));

        var scheme = await ResolveSchemeAsync(bankCountryId, bankCountryAlpha2, suppliedIban, suppliedIbanIsValid, cancellationToken);
        if (scheme is null)
        {
            return PayoutValidationResult.Invalid(BusinessErrorMessage.PayoutCountryNotSupported);
        }

        var swift = SwiftBic.Normalize(input.Swift);
        var swiftError = await ValidateSwiftAsync(swift, bankCountryAlpha2, input.WorkCountryId, cancellationToken);
        if (swiftError is not null)
        {
            return PayoutValidationResult.Invalid(swiftError);
        }

        return scheme switch
        {
            PayoutScheme.CzskDomesticWithIban =>
                ValidateDomestic(input, bankCountryId, bankCountryAlpha2, suppliedIban, swift),
            PayoutScheme.SepaIban =>
                ValidateSepa(input, bankCountryId, bankCountryAlpha2, suppliedIban, suppliedIbanIsValid, swift),
            _ => PayoutValidationResult.Invalid(BusinessErrorMessage.PayoutSchemeNotSupported),
        };
    }

    /// <summary>The catalog stores alpha-2 or alpha-3 depending on how it was seeded — try both.</summary>
    private async Task<string?> ResolveCountryIdAsync(string alpha2, CancellationToken cancellationToken)
    {
        var byAlpha2 = await countryRepository.GetByIsoCodeAsync(alpha2, cancellationToken);
        if (byAlpha2 is not null)
        {
            return byAlpha2.Id;
        }

        var alpha3 = CountryIsoCode.ToAlpha3(alpha2);
        return alpha3 is null ? null : (await countryRepository.GetByIsoCodeAsync(alpha3, cancellationToken))?.Id;
    }

    private async Task<PayoutScheme?> ResolveSchemeAsync(
        string? bankCountryId,
        string bankCountryAlpha2,
        string suppliedIban,
        bool suppliedIbanIsValid,
        CancellationToken cancellationToken)
    {
        var configured = bankCountryId is null
            ? null
            : (await countryConfigurationRepository.GetByCountryIdAsync(bankCountryId, cancellationToken))?.PayoutScheme;

        if (configured is not null)
        {
            return configured;
        }

        return suppliedIbanIsValid && IbanCalculator.CountryPrefixOf(suppliedIban) == bankCountryAlpha2
            ? PayoutScheme.SepaIban
            : null;
    }

    private async Task<string?> ValidateSwiftAsync(
        string swift,
        string bankCountryAlpha2,
        string? workCountryId,
        CancellationToken cancellationToken)
    {
        if (swift.Length == 0)
        {
            var workCountry = string.IsNullOrWhiteSpace(workCountryId)
                ? null
                : await countryRepository.GetByIdAsync(workCountryId, cancellationToken);
            var workCountryAlpha2 = CountryIsoCode.ToAlpha2(workCountry?.IsoCode);

            // Cross-border: the paying bank needs the beneficiary institution named explicitly.
            return workCountryAlpha2 is not null && workCountryAlpha2 != bankCountryAlpha2
                ? BusinessErrorMessage.PayoutSwiftRequired
                : null;
        }

        return SwiftBic.CountryOf(swift) == bankCountryAlpha2
            ? null
            : BusinessErrorMessage.PayoutInvalidSwift;
    }

    private static PayoutValidationResult ValidateDomestic(
        PayoutDetailsInput input,
        string? bankCountryId,
        string bankCountryAlpha2,
        string suppliedIban,
        string swift)
    {
        if (string.IsNullOrWhiteSpace(input.AccountNumber))
        {
            return PayoutValidationResult.Invalid(BusinessErrorMessage.PayoutAccountNumberRequired);
        }

        if (!CzskAccountNumber.IsValidNumber(input.AccountNumber))
        {
            return PayoutValidationResult.Invalid(BusinessErrorMessage.PayoutInvalidAccountNumber);
        }

        var hasPrefix = !string.IsNullOrWhiteSpace(input.AccountPrefix);
        if (hasPrefix && !CzskAccountNumber.IsValidPrefix(input.AccountPrefix))
        {
            return PayoutValidationResult.Invalid(BusinessErrorMessage.PayoutInvalidAccountPrefix);
        }

        var bankCode = input.BankCode?.Trim();
        if (bankCode is not { Length: 4 } || !bankCode.All(char.IsAsciiDigit))
        {
            return PayoutValidationResult.Invalid(BusinessErrorMessage.PayoutInvalidBankCode);
        }

        var accountNumber = CzskAccountNumber.CanonicalizeNumber(input.AccountNumber)!;
        var accountPrefix = hasPrefix ? CzskAccountNumber.CanonicalizePrefix(input.AccountPrefix) : null;
        var derivedIban = IbanCalculator.ComposeCzsk(bankCountryAlpha2, bankCode, accountPrefix, accountNumber);

        // A typed IBAN is a cross-check, never a second source: the parts decide, and disagreement is a
        // typo somewhere the cleaner can see.
        if (suppliedIban.Length > 0 && suppliedIban != derivedIban)
        {
            return PayoutValidationResult.Invalid(BusinessErrorMessage.PayoutIbanMismatch);
        }

        return PayoutValidationResult.Valid(new CanonicalPayoutDetails(
            PayoutScheme.CzskDomesticWithIban,
            bankCountryId,
            accountPrefix,
            accountNumber,
            bankCode,
            derivedIban,
            NullIfEmpty(swift),
            Trimmed(input.BankName),
            Trimmed(input.HolderName)));
    }

    private static PayoutValidationResult ValidateSepa(
        PayoutDetailsInput input,
        string? bankCountryId,
        string bankCountryAlpha2,
        string suppliedIban,
        bool suppliedIbanIsValid,
        string swift)
    {
        if (!suppliedIbanIsValid)
        {
            return PayoutValidationResult.Invalid(BusinessErrorMessage.PayoutInvalidIban);
        }

        if (IbanCalculator.CountryPrefixOf(suppliedIban) != bankCountryAlpha2)
        {
            return PayoutValidationResult.Invalid(BusinessErrorMessage.PayoutIbanCountryMismatch);
        }

        return PayoutValidationResult.Valid(new CanonicalPayoutDetails(
            PayoutScheme.SepaIban,
            bankCountryId,
            null,
            null,
            null,
            suppliedIban,
            NullIfEmpty(swift),
            Trimmed(input.BankName),
            Trimmed(input.HolderName)));
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

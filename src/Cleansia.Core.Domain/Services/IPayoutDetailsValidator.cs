using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Services;

/// <summary>
/// ADR-0034 D4 — real payout validation. Same seam as <see cref="ITaxIdValidator"/> (async,
/// config-driven, returns an i18n key a FluentValidation <c>MustAsync</c> consumes) and the opposite
/// failure mode: <b>this one fails closed</b>. A fail-open tax-id check costs a slightly wrong IČO; a
/// fail-open payout check costs a payroll run into the void or into a stranger's account.
/// <para>Validation and canonicalization are one call: the result carries the stored form, including the
/// server-derived IBAN, so no caller re-derives it and no two callers can disagree.</para>
/// </summary>
public interface IPayoutDetailsValidator
{
    Task<PayoutValidationResult> ValidateAsync(PayoutDetailsInput input, CancellationToken cancellationToken = default);
}

/// <param name="BankCountryId">
/// The country of the <b>bank</b> (ADR-0034 D2). Optional: a mod-97-valid IBAN identifies its own country.
/// </param>
/// <param name="WorkCountryId">
/// The cleaner's work jurisdiction. Only decides whether a cross-border payout needs a BIC.
/// </param>
public sealed record PayoutDetailsInput(
    string? BankCountryId,
    string? WorkCountryId,
    string? AccountPrefix,
    string? AccountNumber,
    string? BankCode,
    string? Iban,
    string? Swift,
    string? BankName,
    string? HolderName);

public sealed record PayoutValidationResult(bool IsValid, string? ErrorKey, CanonicalPayoutDetails? Canonical)
{
    public static PayoutValidationResult Invalid(string errorKey) => new(false, errorKey, null);

    public static PayoutValidationResult Valid(CanonicalPayoutDetails canonical) => new(true, null, canonical);
}

/// <summary>
/// The stored form. <see cref="Iban"/> is the comparison key for every equality, duplicate and fraud
/// check — never the typed parts, which cannot distinguish accounts the IBAN considers identical
/// (ADR-0034 D5.1).
/// </summary>
public sealed record CanonicalPayoutDetails(
    PayoutScheme Scheme,
    string? BankCountryId,
    string? AccountPrefix,
    string? AccountNumber,
    string? BankCode,
    string Iban,
    string? Swift,
    string? BankName,
    string? HolderName);

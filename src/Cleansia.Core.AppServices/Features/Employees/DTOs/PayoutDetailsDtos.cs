using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Employees.DTOs;

/// <summary>
/// ADR-0034 D8 — the payout read contract. Three routes, three DTOs, never one route whose body depends
/// on the caller's role: "masked by default" has to be a server guarantee, not a rendering decision made
/// by five clients, two of which are app-store-gated.
/// <para>These are the ONLY DTOs in the feature surface that may carry a payout identifier, and a frozen
/// surface test asserts it (<c>PayoutDtoSurfaceTests</c>).</para>
/// </summary>
public record MyPayoutDetails(
    PayoutScheme? Scheme,
    PayoutDetailsStatus Status,
    string? BankCountryId,
    string? AccountPrefix,
    string? AccountNumber,
    string? BankCode,
    string? Iban,
    string? Swift,
    string? BankName,
    string? HolderName,
    DateTime? ConfirmedAt);

/// <summary>
/// The admin's default view. It has <b>no unmasked field at all</b> — a client cannot render what it was
/// never sent, so widening this is a schema change a reviewer sees rather than a rendering slip.
/// </summary>
public record MaskedPayoutDetails(
    string EmployeeId,
    PayoutScheme? Scheme,
    PayoutDetailsStatus Status,
    string? BankCountryId,
    string? MaskedAccount,
    string? BankName,
    DateTime? ConfirmedAt,
    DateTime? LastRevealedAt,
    int RevealCount);

public record RevealedPayoutDetails(
    string EmployeeId,
    PayoutScheme? Scheme,
    string? AccountPrefix,
    string? AccountNumber,
    string? BankCode,
    string? Iban,
    string? Swift,
    string? BankName,
    string? HolderName);

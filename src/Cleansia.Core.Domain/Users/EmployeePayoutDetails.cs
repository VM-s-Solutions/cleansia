using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Users;

/// <summary>
/// ADR-0034 — where a cleaner gets paid. One destination, identified several equivalent ways, so the
/// record generalizes along the <see cref="PayoutScheme"/> and pins the count at one
/// (<c>(TenantId, EmployeeId)</c> UNIQUE).
///
/// <para><b>The record holds the data and neither of the two operations that must never be missed
/// (D1.1).</b> The profile-completeness gate reads <see cref="Employee.HasPayoutDetails"/> — a scalar on
/// the employee row, materialized by every loader — and erasure is an id-keyed delete owned by the GDPR
/// service, not a navigation walk. There is no lazy loading in this repository, so an invariant hung on
/// this navigation would be load-order-dependent.</para>
///
/// <para>Mutated in place, never tombstoned (D1.2): the row <i>is</i> the current destination, so nothing
/// deactivates it and no reader needs an <c>IsActive</c> predicate. Erasure hard-deletes.</para>
///
/// <para>Which identifier fields are meaningful is decided by <see cref="Scheme"/>; which are
/// <i>required</i> is decided per scheme by the payout validator, not by the column's nullability
/// (D4/D5).</para>
/// </summary>
public class EmployeePayoutDetails : Auditable, ITenantEntity
{
    [Required]
    [MaxLength(26)]
    public string EmployeeId { get; private set; } = default!;
    public Employee? Employee { get; private set; }

    /// <summary>Null ⇒ unusable for payout: the issuance block refuses it (D7).</summary>
    public PayoutScheme? Scheme { get; private set; }

    /// <summary>
    /// The country of the <b>bank</b>, not of residence, work or registration — a bank account's format is
    /// decided by the bank (D2). Pre-filled from the employee's work country, but the cleaner owns it.
    /// </summary>
    [MaxLength(26)]
    public string? BankCountryId { get; private set; }
    public Country? BankCountry { get; private set; }

    /// <summary>Zero-padded canonical CZ/SK account prefix. Leading zeros are canonicalization, not identity (D5.1).</summary>
    [MaxLength(6)]
    public string? AccountPrefix { get; private set; }

    /// <summary>Zero-padded canonical CZ/SK account number (D5.1).</summary>
    [MaxLength(10)]
    public string? AccountNumber { get; private set; }

    [MaxLength(4)]
    public string? BankCode { get; private set; }

    /// <summary>
    /// Server-derived for <see cref="PayoutScheme.CzskDomesticWithIban"/>, collected for
    /// <see cref="PayoutScheme.SepaIban"/>. The comparison key for every equality, duplicate and fraud
    /// check — never the typed parts (D5.1/D5.2).
    /// </summary>
    [MaxLength(34)]
    public string? Iban { get; private set; }

    [MaxLength(11)]
    public string? Swift { get; private set; }

    [MaxLength(100)]
    public string? BankName { get; private set; }

    /// <summary>The beneficiary name as the bank knows it, which may differ from the platform's. PII.</summary>
    [MaxLength(200)]
    public string? HolderName { get; private set; }

    /// <summary>A payment-provider account id (<c>acct_…</c>) for <see cref="PayoutScheme.ProviderPayoutToken"/>. An id, never a card number (D9a).</summary>
    [MaxLength(100)]
    public string? ProviderAccountRef { get; private set; }

    [Required]
    public PayoutDetailsStatus Status { get; private set; }

    /// <summary>When the details last passed the real validator.</summary>
    public DateTime? ConfirmedAt { get; private set; }

    /// <summary>
    /// Stamped by the admin reveal command. The reveal is a command precisely so the existing audit
    /// engine records it — the audit trail is the compensating control for storing this in plaintext (D6/D8.4).
    /// </summary>
    public DateTime? LastRevealedAt { get; private set; }

    public int RevealCount { get; private set; }

    private EmployeePayoutDetails() { }

    public static EmployeePayoutDetails Create(
        string employeeId,
        PayoutScheme? scheme,
        string? bankCountryId,
        PayoutDetailsStatus status,
        string? accountPrefix = null,
        string? accountNumber = null,
        string? bankCode = null,
        string? iban = null,
        string? swift = null,
        string? bankName = null,
        string? holderName = null,
        string? providerAccountRef = null,
        DateTime? confirmedAt = null)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
        {
            throw new ArgumentException("EmployeeId is required", nameof(employeeId));
        }

        return new EmployeePayoutDetails
        {
            EmployeeId = employeeId,
            Scheme = scheme,
            BankCountryId = string.IsNullOrEmpty(bankCountryId) ? null : bankCountryId,
            Status = status,
            AccountPrefix = accountPrefix,
            AccountNumber = accountNumber,
            BankCode = bankCode,
            Iban = iban,
            Swift = swift,
            BankName = bankName,
            HolderName = holderName,
            ProviderAccountRef = providerAccountRef,
            ConfirmedAt = confirmedAt,
        };
    }

    /// <summary>
    /// Replace the destination in place. There is exactly one payout record per cleaner and it is
    /// superseded, never versioned — history belongs in the admin audit log (D1.2).
    /// </summary>
    public EmployeePayoutDetails Update(
        PayoutScheme? scheme,
        string? bankCountryId,
        PayoutDetailsStatus status,
        string? accountPrefix = null,
        string? accountNumber = null,
        string? bankCode = null,
        string? iban = null,
        string? swift = null,
        string? bankName = null,
        string? holderName = null,
        string? providerAccountRef = null,
        DateTime? confirmedAt = null)
    {
        Scheme = scheme;
        BankCountryId = string.IsNullOrEmpty(bankCountryId) ? null : bankCountryId;
        Status = status;
        AccountPrefix = accountPrefix;
        AccountNumber = accountNumber;
        BankCode = bankCode;
        Iban = iban;
        Swift = swift;
        BankName = bankName;
        HolderName = holderName;
        ProviderAccountRef = providerAccountRef;
        ConfirmedAt = confirmedAt;
        return this;
    }

    public EmployeePayoutDetails MarkRevealed(DateTime revealedAtUtc)
    {
        LastRevealedAt = revealedAtUtc;
        RevealCount++;
        return this;
    }
}

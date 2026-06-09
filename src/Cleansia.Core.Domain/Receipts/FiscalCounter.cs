using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Fiscal.Abstractions;

namespace Cleansia.Core.Domain.Receipts;

/// <summary>
/// Durable per-issuer fiscal sequence counter. One row per
/// <c>(TenantId, Year, IssuerScope)</c> holds the last allocated number; the next allocation is
/// produced by an atomic increment in the repository (never by counting receipts), so the sequence
/// is gapless-monotonic per issuer as DE TSE / AT RKSV / ES VeriFactu legally require.
///
/// <para><b>IssuerScope</b> binds gaplessness to the legal counting unit per regime — the TSE / cash
/// register / issuer, NOT merely the tenant: CZ EET and SK eKasa count per provider per tenant, so
/// the scope is the fiscal provider key; DE TSE counts per physical TSE, so the scope carries the TSE
/// identity. The string is the extension point — a single fiscal provider key today, a
/// provider-plus-device key once multi-TSE config lands — and never empty (use
/// <see cref="DefaultIssuerScope"/> when a regime scopes only per tenant).</para>
///
/// <para><b>Year semantics differ per regime.</b> CZ EET-style numbering resets annually, so the
/// calendar year keys the counter. DE TSE's transaction counter does NOT reset at the year boundary;
/// such regimes key on <see cref="NoAnnualResetYear"/> so the same row keeps incrementing across
/// years.</para>
/// </summary>
public class FiscalCounter : Auditable, ITenantEntity
{
    public const string DefaultIssuerScope = FiscalSequenceScope.DefaultIssuerScope;

    public const int NoAnnualResetYear = FiscalSequenceScope.NoAnnualResetYear;

    public int Year { get; private set; }

    [Required]
    [MaxLength(100)]
    public string IssuerScope { get; private set; } = default!;

    public long Value { get; private set; }

    public static FiscalCounter Create(int year, string issuerScope, long value = 0)
    {
        return new FiscalCounter
        {
            Year = year,
            IssuerScope = issuerScope,
            Value = value
        };
    }
}

using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.EmployeePayroll;

/// <summary>
/// Durable counter behind a payout invoice's two references (ADR-0046, per-company since the
/// 2026-09-15 ruling that each operating company numbers its own payout invoices). One row per
/// <c>(TenantId, Year, Scope)</c> holds the last allocated ordinal; the next one is produced by an
/// atomic UPSERT in <c>IPayoutReferenceCounterRepository.AllocateNextAsync</c>, never by counting
/// invoices.
///
/// <para><b>Scope</b> names the series: the ten-digit <i>variabilní symbol</i> and the
/// <c>INV-YYYY-NNNNNN</c> invoice number are two independent sequences on the same table, the way
/// <see cref="Receipts.FiscalCounter"/>'s <c>IssuerScope</c> keys one row per issuer. Two companies'
/// first invoices of a year carry the same strings by construction, and the per-company unique
/// indexes on <c>EmployeeInvoices</c> are what make that a fact rather than a collision.</para>
///
/// <para><b>Deliberately gappy</b>, unlike <see cref="Receipts.FiscalCounter"/>, whose contract is
/// gaplessness for CZ EET / DE TSE / AT RKSV. A payment reference is not a fiscal document number,
/// so an allocation whose invoice never commits is simply lost. A design that never gaps and
/// sometimes duplicates is strictly worse than one that sometimes gaps and never duplicates.</para>
/// </summary>
public class PayoutReferenceCounter : TenantAuditable
{
    public const string VariableSymbolScope = "VariableSymbol";

    public const string InvoiceNumberScope = "InvoiceNumber";

    public int Year { get; private set; }

    [Required]
    [MaxLength(20)]
    public string Scope { get; private set; } = default!;

    public long Value { get; private set; }
}

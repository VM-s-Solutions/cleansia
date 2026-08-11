using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.EmployeePayroll;

/// <summary>
/// Durable counter behind a payout invoice's <i>variabilní symbol</i> (ADR-0046). One row per
/// calendar year holds the last allocated ordinal; the next one is produced by an atomic UPSERT in
/// <c>IPayoutReferenceCounterRepository.AllocateNextAsync</c>, never by counting invoices.
///
/// <para><b>The key is <c>(Year)</c> and nothing else</b> — no scope column, no tenant term, and
/// deliberately NOT <see cref="ITenantEntity"/>. The reference namespace is global because the
/// shipped unique index on <c>EmployeeInvoices.VariableSymbol</c> is on the bare column; a
/// tenant-keyed counter under it would make two tenants both allocate ordinal 1 and turn a tenancy
/// fact into a 500 on the payroll path. A non-nullable <c>int</c> arbiter also cannot reproduce the
/// nulls-distinct collapse that <see cref="Receipts.FiscalCounter"/>'s index has to work around with
/// <c>AreNullsDistinct(false)</c>. Adding any second key column re-arms that class of bug —
/// ADR-0046 §D2.1 is the gate, and §D3.2 writes down the flip if multi-tenancy ever forces one.</para>
///
/// <para><b>Deliberately gappy</b>, unlike <see cref="Receipts.FiscalCounter"/>, whose contract is
/// gaplessness for CZ EET / DE TSE / AT RKSV. A payment reference is not a fiscal document number,
/// so an allocation whose invoice never commits is simply lost. A design that never gaps and
/// sometimes duplicates is strictly worse than one that sometimes gaps and never duplicates.</para>
/// </summary>
public class PayoutReferenceCounter : BaseEntity
{
    public int Year { get; private set; }

    public long Value { get; private set; }

    [Required]
    [MaxLength(255)]
    public string CreatedBy { get; private set; } = default!;

    public DateTimeOffset CreatedOn { get; private set; }

    [MaxLength(255)]
    public string? UpdatedBy { get; private set; }

    public DateTimeOffset? UpdatedOn { get; private set; }
}

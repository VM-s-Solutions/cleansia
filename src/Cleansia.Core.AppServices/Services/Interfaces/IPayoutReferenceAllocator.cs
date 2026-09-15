using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Claims the next payout-invoice references for the AMBIENT operating company (ADR-0046; per company
/// since the 2026-09-15 ruling). Role card: <c>docs/domain/roles/payout-reference-allocator.md</c>.
///
/// <para>It knows the calendar year and the ambient company, and nothing else — not the invoice, not
/// the pay period. The year is the year of ALLOCATION, so a December period closed on 2 January
/// produces a <c>2027…</c> reference.</para>
///
/// <para>Callers MUST NOT invoke this inside an explicit transaction, and MUST allocate BEFORE
/// constructing the invoice — the number is claimed before any row or document can carry it.</para>
/// </summary>
public interface IPayoutReferenceAllocator
{
    /// <summary>
    /// Returns a ten-digit <c>YYYYNNNNNN</c> <i>variabilní symbol</i> whose first digit is never
    /// <c>0</c>, or a failure carrying <c>payroll.invoice.reference_capacity_exhausted</c> when the
    /// company's 999 999 references of the year are used up. Within a year there is no remedy but
    /// the year.
    /// </summary>
    Task<BusinessResult<string>> AllocateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns the company's next <c>INV-YYYY-NNNNNN</c> invoice number, or the same capacity failure
    /// as <see cref="AllocateAsync"/>. A separate series from the variable symbol: the owner ruled the
    /// two may never coincide, and a non-numeric shape is what makes that structural.
    /// </summary>
    Task<BusinessResult<string>> AllocateInvoiceNumberAsync(CancellationToken cancellationToken);
}

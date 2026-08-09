using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Claims the next payout-invoice <i>variabilní symbol</i> (ADR-0046). Role card:
/// <c>agents/knowledge/roles/payout-reference-allocator.md</c>.
///
/// <para>It knows the calendar year and nothing else — not the tenant, not the invoice, not the pay
/// period. The year is the year of ALLOCATION, so a December period closed on 2 January produces a
/// <c>2027…</c> reference.</para>
///
/// <para>Callers MUST NOT invoke this inside an explicit transaction, and MUST allocate BEFORE
/// constructing the invoice — the number is claimed before any row or document can carry it.</para>
/// </summary>
public interface IPayoutReferenceAllocator
{
    /// <summary>
    /// Returns a ten-digit <c>YYYYNNNNNN</c> reference whose first digit is never <c>0</c>, or a
    /// failure carrying <c>payroll.invoice.reference_capacity_exhausted</c> when the year's 999 999
    /// references are used up. Within a year there is no remedy but the year.
    /// </summary>
    Task<BusinessResult<string>> AllocateAsync(CancellationToken cancellationToken);
}

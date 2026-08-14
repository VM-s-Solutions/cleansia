namespace Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;

public record PeriodPaySummaryDto(
    string PayPeriodId,
    string PayPeriodLabel,
    string EmployeeId,
    string EmployeeName,
    int TotalOrders,
    decimal TotalBasePay,
    decimal TotalExtrasPay,
    decimal TotalExpensesPay,
    decimal TotalBonusPay,
    decimal TotalDeductionPay,
    decimal GrandTotal,
    bool HasInvoice,
    string? InvoiceId,
    IEnumerable<OrderEmployeePayDto> OrderPays,
    /// <summary>
    /// The currency every amount above is denominated in. Clients must render this rather than a
    /// hardcoded symbol — the partner "My Pay" screen printed a literal "Kč" until 2026-08-14, which
    /// would disagree with the cleaner's own payout invoice the day a second country configuration
    /// exists. That invoice is a document they file with their tax return.
    ///
    /// <para>Sourced from the INVOICE when the period has one, so the two screens cannot diverge on a
    /// closed period, and from the employee's work country only when it does not. Nullable + defaulted
    /// so it is additive on the wire.</para>
    /// → /flows/pay-and-payouts
    /// </summary>
    string? CurrencyCode = null);

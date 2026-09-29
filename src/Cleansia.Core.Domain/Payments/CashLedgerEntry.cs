using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Payments;

/// <summary>
/// One movement of the company's cash in a cleaner's hands (owner ruling 2026-09-28, decision 23): a
/// collection at the door adds to what the cleaner holds, a remittance to the company, a write-off by an
/// administrator or a set-off against the cleaner's invoice takes from it. <see cref="Amount"/> is signed
/// that way, so the cash a cleaner holds in a currency is the sum of their entries in it. An entry's amount
/// is never edited; a correction is a new entry.
/// </summary>
public class CashLedgerEntry : TenantAuditable
{
    [Required]
    [MaxLength(26)]
    public string EmployeeId { get; private set; } = default!;
    public Employee? Employee { get; private set; }

    /// <summary>The order whose cash was collected; a remittance or a write-off names none.</summary>
    [MaxLength(26)]
    public string? OrderId { get; private set; }
    public Order? Order { get; private set; }

    [Required]
    [MaxLength(26)]
    public string CurrencyId { get; private set; } = default!;
    public Currency? Currency { get; private set; }

    public CashLedgerEntryKind Kind { get; private set; }

    public decimal Amount { get; private set; }

    public DateTime OccurredAt { get; private set; }

    [MaxLength(500)]
    public string? Note { get; private set; }

    /// <summary>
    /// Set on the entry that took the cleaner's cash in its currency above zero, once they were asked to
    /// hand that cash over; a later balance that starts afresh from zero is asked about again.
    /// </summary>
    public DateTime? RemittanceRequestedAt { get; private set; }

    private CashLedgerEntry()
    {
    }

    /// <summary>A company's float cap of zero is no cap; above a set one, cash jobs are hidden from the cleaner.</summary>
    public static bool HoldsAboveFloatCap(decimal cashHeld, int floatCap) => floatCap > 0 && cashHeld > floatCap;

    public static CashLedgerEntry ForCollection(Order order) =>
        new()
        {
            EmployeeId = order.CollectedByEmployeeId
                ?? throw new InvalidOperationException($"Order {order.Id} records no cleaner who collected its cash."),
            OrderId = order.Id,
            CurrencyId = order.CurrencyId,
            Kind = CashLedgerEntryKind.Collection,
            Amount = order.CashCollectedAmount
                ?? throw new InvalidOperationException($"Order {order.Id} records no cash amount."),
            OccurredAt = order.CashCollectedAt
                ?? throw new InvalidOperationException($"Order {order.Id} records no cash collection time."),
        };

    public static CashLedgerEntry ForRemittance(
        string employeeId, string currencyId, decimal amount, string? note, DateTime occurredAt) =>
        new()
        {
            EmployeeId = employeeId,
            CurrencyId = currencyId,
            Kind = CashLedgerEntryKind.Remittance,
            Amount = -amount,
            OccurredAt = occurredAt,
            Note = note,
        };

    public static CashLedgerEntry ForWriteOff(
        string employeeId, string currencyId, decimal amount, string note, DateTime occurredAt) =>
        new()
        {
            EmployeeId = employeeId,
            CurrencyId = currencyId,
            Kind = CashLedgerEntryKind.WriteOff,
            Amount = -amount,
            OccurredAt = occurredAt,
            Note = note,
        };

    /// <summary>The cash <see cref="EmployeeInvoice.SetOffCash"/> took off the invoice's transfer, when the invoice was issued.</summary>
    public static CashLedgerEntry ForSetOff(EmployeeInvoice invoice) =>
        new()
        {
            EmployeeId = invoice.EmployeeId,
            CurrencyId = invoice.CurrencyId,
            Kind = CashLedgerEntryKind.SetOff,
            Amount = -invoice.CashSetOffAmount,
            OccurredAt = invoice.GeneratedAt,
            Note = invoice.InvoiceNumber,
        };

    /// <summary>A cancelled invoice transfers nothing, so the cash it set off is the cleaner's to hold again.</summary>
    public static CashLedgerEntry ForSetOffReversal(EmployeeInvoice invoice) =>
        new()
        {
            EmployeeId = invoice.EmployeeId,
            CurrencyId = invoice.CurrencyId,
            Kind = CashLedgerEntryKind.SetOff,
            Amount = invoice.CashSetOffAmount,
            OccurredAt = invoice.CancelledAt
                ?? throw new InvalidOperationException($"Invoice {invoice.Id} is not cancelled."),
            Note = invoice.InvoiceNumber,
        };

    public void MarkRemittanceRequested(DateTime requestedAt) => RemittanceRequestedAt = requestedAt;
}

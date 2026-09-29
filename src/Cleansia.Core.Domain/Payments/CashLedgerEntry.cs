using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Payments;

/// <summary>
/// One movement of the company's cash in a cleaner's hands (owner ruling 2026-09-28, decision 23): a
/// collection at the door adds to what the cleaner holds, a remittance to the company or a write-off by an
/// administrator takes from it. <see cref="Amount"/> is signed that way, so the cash a cleaner holds in a
/// currency is the sum of their entries in it. Entries are never edited; a correction is a new entry.
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

    private CashLedgerEntry()
    {
    }

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
}

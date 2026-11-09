using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.EmployeePayroll;

public class PayPeriod : TenantAuditable
{
    /// <summary>
    /// The days, start and end inclusive, that both cleaner agreements promise a pay period runs (owner decision
    /// 2026-10-04). The hosts read the length from PayPeriodClosing:LengthInDays, which defaults to this.
    /// </summary>
    public const int LengthInDays = 14;

    [Required]
    public DateOnly StartDate { get; private set; }

    [Required]
    public DateOnly EndDate { get; private set; }

    [Required]
    public PayPeriodStatus Status { get; private set; } = PayPeriodStatus.Open;

    [MaxLength(500)]
    public string? Notes { get; private set; }

    public DateTime? ClosedAt { get; private set; }

    public string? ClosedBy { get; private set; }

    public DateTime? PaidAt { get; private set; }

    private ICollection<EmployeeInvoice> _invoices = [];
    public IReadOnlyCollection<EmployeeInvoice> Invoices => _invoices.ToList().AsReadOnly();

    private ICollection<OrderEmployeePay> _orderPays = [];
    public IReadOnlyCollection<OrderEmployeePay> OrderPays => _orderPays.ToList().AsReadOnly();

    public static PayPeriod Create(DateOnly startDate, DateOnly endDate, string? notes = null)
    {
        if (endDate <= startDate)
        {
            throw new ArgumentException("End date must be after start date", nameof(endDate));
        }

        var duration = endDate.DayNumber - startDate.DayNumber;
        if (duration < 1 || duration > 31)
        {
            throw new ArgumentException("Pay period must be between 1 and 31 days", nameof(endDate));
        }

        return new PayPeriod
        {
            StartDate = startDate,
            EndDate = endDate,
            Status = PayPeriodStatus.Open,
            Notes = notes
        };
    }

    public static PayPeriod CreateBiWeekly(DateOnly startDate, int lengthInDays = LengthInDays, string? notes = null)
    {
        var endDate = startDate.AddDays(lengthInDays - 1);
        return Create(startDate, endDate, notes);
    }

    public static PayPeriod CreateMonthlyFirstHalf(int year, int month)
    {
        var startDate = new DateOnly(year, month, 1);
        var endDate = new DateOnly(year, month, 14);
        return Create(startDate, endDate, $"First half of {startDate:MMMM yyyy}");
    }

    public static PayPeriod CreateMonthlySecondHalf(int year, int month)
    {
        var startDate = new DateOnly(year, month, 15);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var endDate = new DateOnly(year, month, daysInMonth);
        return Create(startDate, endDate, $"Second half of {startDate:MMMM yyyy}");
    }

    public PayPeriod Update(DateOnly startDate, DateOnly endDate, int maxLengthInDays, string? notes = null)
    {
        if (Status != PayPeriodStatus.Open)
        {
            throw new InvalidOperationException($"Cannot update period in status {Status}");
        }

        if (endDate <= startDate)
        {
            throw new ArgumentException("End date must be after start date", nameof(endDate));
        }

        if (endDate.DayNumber - startDate.DayNumber + 1 > maxLengthInDays)
        {
            throw new ArgumentException($"Pay period must be at most {maxLengthInDays} days", nameof(endDate));
        }

        StartDate = startDate;
        EndDate = endDate;

        if (notes != null)
        {
            Notes = notes;
        }

        return this;
    }

    public PayPeriod Close(string closedBy, string? notes = null)
    {
        if (Status != PayPeriodStatus.Open)
        {
            throw new InvalidOperationException($"Cannot close period in status {Status}");
        }

        Status = PayPeriodStatus.Closed;
        ClosedAt = DateTime.UtcNow;
        ClosedBy = closedBy;

        if (notes != null)
        {
            Notes = notes;
        }

        return this;
    }

    public PayPeriod MarkAsPaid()
    {
        if (Status != PayPeriodStatus.Closed)
        {
            throw new InvalidOperationException($"Cannot mark as paid. Period must be closed first. Current status: {Status}");
        }

        Status = PayPeriodStatus.Paid;
        PaidAt = DateTime.UtcNow;

        return this;
    }

    public PayPeriod Reopen(string? notes = null)
    {
        if (Status == PayPeriodStatus.Paid)
        {
            throw new InvalidOperationException("Cannot reopen a paid period");
        }

        Status = PayPeriodStatus.Open;
        ClosedAt = null;
        ClosedBy = null;

        if (notes != null)
        {
            Notes = notes;
        }

        return this;
    }

    public int GetPeriodDays()
    {
        return EndDate.DayNumber - StartDate.DayNumber + 1;
    }

    public string GetPeriodLabel()
    {
        return $"{StartDate:MMM dd} - {EndDate:MMM dd, yyyy}";
    }
}

using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.EmployeePayroll;

public class PayPeriod : Auditable, ITenantEntity
{
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
        if (duration < 7 || duration > 31)
        {
            throw new ArgumentException("Pay period must be between 7 and 31 days", nameof(endDate));
        }

        return new PayPeriod
        {
            StartDate = startDate,
            EndDate = endDate,
            Status = PayPeriodStatus.Open,
            Notes = notes
        };
    }

    public static PayPeriod CreateBiWeekly(DateOnly startDate, string? notes = null)
    {
        var endDate = startDate.AddDays(13); // 14 days total (inclusive)
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

    public PayPeriod Update(DateOnly startDate, DateOnly endDate, string? notes = null)
    {
        if (Status != PayPeriodStatus.Open)
        {
            throw new InvalidOperationException($"Cannot update period in status {Status}");
        }

        if (endDate <= startDate)
        {
            throw new ArgumentException("End date must be after start date", nameof(endDate));
        }

        var duration = endDate.DayNumber - startDate.DayNumber;
        if (duration < 7 || duration > 31)
        {
            throw new ArgumentException("Pay period must be between 7 and 31 days", nameof(endDate));
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

    public bool IsWithinPeriod(DateOnly date)
    {
        return date >= StartDate && date <= EndDate;
    }

    public int GetPeriodDays()
    {
        return EndDate.DayNumber - StartDate.DayNumber + 1;
    }

    public bool OverlapsWith(PayPeriod other)
    {
        return StartDate <= other.EndDate && EndDate >= other.StartDate;
    }

    public string GetPeriodLabel()
    {
        return $"{StartDate:MMM dd} - {EndDate:MMM dd, yyyy}";
    }
}

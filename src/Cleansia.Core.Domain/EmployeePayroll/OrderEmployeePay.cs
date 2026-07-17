using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.EmployeePayroll;

public class OrderEmployeePay : Auditable, ITenantEntity
{
    [Required]
    public string OrderId { get; private set; } = default!;
    public Order? Order { get; private set; }

    [Required]
    public string EmployeeId { get; private set; } = default!;
    public Employee? Employee { get; private set; }

    [Required]
    public string PayPeriodId { get; private set; } = default!;
    public PayPeriod? PayPeriod { get; private set; }

    [Required]
    public decimal BasePay { get; private set; }

    public decimal ExtrasPay { get; private set; } = 0;

    public decimal ExpensesPay { get; private set; } = 0;

    public decimal BonusPay { get; private set; } = 0;

    public decimal DeductionPay { get; private set; } = 0;

    // The pay-config min/max bounds captured at calculation time. TotalPay is the CLAMPED core
    // (base+extras+expenses bounded to [MinPay, MaxPay]) plus bonus minus deduction. Persisting the
    // bounds is what lets the mutators below re-apply the clamp — recomputing TotalPay purely from the
    // raw components would silently undo it (T-0362). 0 == unbounded on that edge (mirrors the
    // calculator's `> 0` guard in PayCalculatorExtensions.ApplyMinMaxClamp).
    public decimal MinPay { get; private set; } = 0;

    public decimal MaxPay { get; private set; } = 0;

    [Required]
    public decimal TotalPay { get; private set; }

    [MaxLength(1000)]
    public string? Notes { get; private set; }

    [MaxLength(2000)]
    public string? PayBreakdown { get; private set; }

    public bool IsApproved { get; private set; } = false;

    public DateTime? ApprovedAt { get; private set; }

    public string? ApprovedBy { get; private set; }

    public string? EmployeeInvoiceId { get; private set; }
    public EmployeeInvoice? EmployeeInvoice { get; private set; }

    public static OrderEmployeePay Create(
        string orderId,
        string employeeId,
        string payPeriodId,
        decimal basePay,
        decimal extrasPay = 0,
        decimal expensesPay = 0,
        decimal bonusPay = 0,
        decimal deductionPay = 0,
        decimal totalPay = 0,
        decimal minPay = 0,
        decimal maxPay = 0,
        string? notes = null,
        string? payBreakdown = null)
    {
        if (basePay < 0)
        {
            throw new ArgumentException("Base pay cannot be negative", nameof(basePay));
        }

        if (extrasPay < 0)
        {
            throw new ArgumentException("Extras pay cannot be negative", nameof(extrasPay));
        }

        if (expensesPay < 0)
        {
            throw new ArgumentException("Expenses pay cannot be negative", nameof(expensesPay));
        }

        if (totalPay < 0)
        {
            totalPay = 0;
        }

        return new OrderEmployeePay
        {
            OrderId = orderId,
            EmployeeId = employeeId,
            PayPeriodId = payPeriodId,
            BasePay = basePay,
            ExtrasPay = extrasPay,
            ExpensesPay = expensesPay,
            BonusPay = bonusPay,
            DeductionPay = deductionPay,
            MinPay = minPay,
            MaxPay = maxPay,
            TotalPay = totalPay,
            Notes = notes,
            PayBreakdown = payBreakdown
        };
    }

    public OrderEmployeePay UpdatePay(
        decimal basePay,
        decimal extrasPay,
        decimal expensesPay,
        decimal bonusPay,
        decimal deductionPay,
        string? notes = null)
    {
        if (IsApproved)
        {
            throw new InvalidOperationException("Cannot update pay after approval");
        }

        BasePay = basePay;
        ExtrasPay = extrasPay;
        ExpensesPay = expensesPay;
        BonusPay = bonusPay;
        DeductionPay = deductionPay;
        RecomputeTotalPay();

        if (notes != null)
        {
            Notes = notes;
        }

        return this;
    }

    public OrderEmployeePay AddBonus(decimal bonusAmount, string? notes = null)
    {
        if (bonusAmount < 0)
        {
            throw new ArgumentException("Bonus amount cannot be negative", nameof(bonusAmount));
        }

        BonusPay += bonusAmount;
        RecomputeTotalPay();

        if (notes != null)
        {
            Notes = string.IsNullOrEmpty(Notes) ? notes : $"{Notes}; {notes}";
        }

        return this;
    }

    public OrderEmployeePay AddDeduction(decimal deductionAmount, string? notes = null)
    {
        if (deductionAmount < 0)
        {
            throw new ArgumentException("Deduction amount cannot be negative", nameof(deductionAmount));
        }

        DeductionPay += deductionAmount;
        RecomputeTotalPay();

        if (notes != null)
        {
            Notes = string.IsNullOrEmpty(Notes) ? notes : $"{Notes}; {notes}";
        }

        return this;
    }

    /// <summary>
    /// The single source of truth for TotalPay once components change: clamp the core
    /// (base+extras+expenses) to the persisted [MinPay, MaxPay] bounds — the SAME clamp the calculator
    /// applied at creation — then add bonus and subtract deduction, floored at 0. Reuses
    /// <see cref="PayCalculatorExtensions.ApplyMinMaxClamp"/> so the &gt;0-guard and min&gt;max-throw
    /// semantics stay identical to calc time (T-0362).
    /// </summary>
    private void RecomputeTotalPay()
    {
        var clampedCore = PayCalculatorExtensions.ApplyMinMaxClamp(
            BasePay + ExtrasPay + ExpensesPay, MinPay, MaxPay);
        TotalPay = Math.Max(0m, clampedCore + BonusPay - DeductionPay);
    }

    public OrderEmployeePay Approve(string approvedBy)
    {
        if (IsApproved)
        {
            throw new InvalidOperationException("Pay already approved");
        }

        IsApproved = true;
        ApprovedAt = DateTime.UtcNow;
        ApprovedBy = approvedBy;

        return this;
    }

    public OrderEmployeePay AssignToInvoice(string employeeInvoiceId)
    {
        EmployeeInvoiceId = employeeInvoiceId;
        return this;
    }

    public OrderEmployeePay SetPayBreakdown(string breakdown)
    {
        PayBreakdown = breakdown;
        return this;
    }

    public OrderEmployeePay Anonymize()
    {
        Notes = Notes is null ? null : AnonymizationMarker.Value;
        return this;
    }
}

using Cleansia.Core.Domain.EmployeePayroll;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// Pins the min/max clamp invariant across the OrderEmployeePay mutators (T-0362). The pay the
/// calculator persists is the CLAMPED core — <c>clamp(base+extras+expenses, MinPay, MaxPay)</c> — plus
/// bonus minus deduction. Before the fix, <see cref="OrderEmployeePay.UpdatePay"/>,
/// <see cref="OrderEmployeePay.AddBonus"/> and <see cref="OrderEmployeePay.AddDeduction"/> recomputed
/// TotalPay from the raw components and silently dropped the clamp, so a bonus on a capped order (or a
/// deduction on a floored one) paid out the wrong amount. These tests assert the clamp survives every
/// mutation, with EXACT <c>decimal</c> assertions — this is money paid to cleaners.
/// </summary>
public class OrderEmployeePayClampTests
{
    private const string OrderId = "01J0000000000000000000ORDR";
    private const string EmployeeId = "01J000000000000000000EMPL";
    private const string PayPeriodId = "01J0000000000000000000PRD";

    private static OrderEmployeePay Create(
        decimal basePay,
        decimal extrasPay = 0,
        decimal expensesPay = 0,
        decimal minPay = 0,
        decimal maxPay = 0)
    {
        // totalPay mirrors what CalculateOrderPay hands in: the clamped core at creation time.
        var core = basePay + extrasPay + expensesPay;
        var clamped = core;
        if (minPay > 0 && clamped < minPay) clamped = minPay;
        if (maxPay > 0 && clamped > maxPay) clamped = maxPay;

        return OrderEmployeePay.Create(
            orderId: OrderId,
            employeeId: EmployeeId,
            payPeriodId: PayPeriodId,
            basePay: basePay,
            extrasPay: extrasPay,
            expensesPay: expensesPay,
            totalPay: clamped,
            minPay: minPay,
            maxPay: maxPay);
    }

    // ── AddBonus keeps the ceiling on the core ──

    [Fact]
    public void AddBonus_On_Capped_Order_Adds_Bonus_Above_The_Clamped_Core_Not_The_Raw_Core()
    {
        // Core = 200, capped to 150. A 50 bonus must pay 150 + 50 = 200, NOT 200 + 50 = 250.
        var pay = Create(basePay: 200m, maxPay: 150m);
        Assert.Equal(150m, pay.TotalPay);

        pay.AddBonus(50m);

        Assert.Equal(200m, pay.TotalPay);
    }

    [Fact]
    public void AddBonus_Below_The_Cap_Is_Unaffected_By_The_Clamp()
    {
        // Core = 100, cap = 150 → no clamp. Bonus 30 → 130.
        var pay = Create(basePay: 100m, maxPay: 150m);
        pay.AddBonus(30m);

        Assert.Equal(130m, pay.TotalPay);
    }

    // ── AddDeduction keeps the floor on the core ──

    [Fact]
    public void AddDeduction_On_Floored_Order_Deducts_From_The_Clamped_Core_Not_The_Raw_Core()
    {
        // Core = 50, floored up to 100. A 30 deduction must pay 100 - 30 = 70, NOT 50 - 30 = 20.
        var pay = Create(basePay: 50m, minPay: 100m);
        Assert.Equal(100m, pay.TotalPay);

        pay.AddDeduction(30m);

        Assert.Equal(70m, pay.TotalPay);
    }

    [Fact]
    public void AddDeduction_Beyond_The_Clamped_Core_Floors_TotalPay_At_Zero()
    {
        var pay = Create(basePay: 50m, minPay: 100m);

        pay.AddDeduction(500m);

        Assert.Equal(0m, pay.TotalPay);
    }

    // ── UpdatePay re-clamps the new components ──

    [Fact]
    public void UpdatePay_ReClamps_The_New_Core_To_The_Persisted_Bounds()
    {
        var pay = Create(basePay: 100m, minPay: 80m, maxPay: 150m);

        // New core 300 exceeds the 150 cap → clamp to 150, then +20 bonus -10 deduction = 160.
        pay.UpdatePay(basePay: 300m, extrasPay: 0m, expensesPay: 0m, bonusPay: 20m, deductionPay: 10m);

        Assert.Equal(160m, pay.TotalPay);
    }

    [Fact]
    public void UpdatePay_Below_The_Floor_Is_Raised_To_The_Minimum()
    {
        var pay = Create(basePay: 100m, minPay: 80m, maxPay: 150m);

        // New core 40 is below the 80 floor → clamp up to 80, no bonus/deduction.
        pay.UpdatePay(basePay: 40m, extrasPay: 0m, expensesPay: 0m, bonusPay: 0m, deductionPay: 0m);

        Assert.Equal(80m, pay.TotalPay);
    }

    // ── Unbounded default path is unchanged (regression guard) ──

    [Fact]
    public void Unbounded_Config_Adds_Bonus_And_Deduction_Straight_To_The_Raw_Core()
    {
        // MinPay = MaxPay = 0 → no clamp on either edge; behavior identical to the raw sum.
        var pay = Create(basePay: 200m, extrasPay: 30m, expensesPay: 10m);

        pay.AddBonus(40m);
        pay.AddDeduction(15m);

        // 240 core + 40 bonus - 15 deduction = 265.
        Assert.Equal(265m, pay.TotalPay);
    }

    // ── Defensive: an inconsistent stored bound throws, matching calc-time semantics ──

    [Fact]
    public void Mutating_With_Inconsistent_Bounds_Throws_Just_Like_The_Calculator()
    {
        var pay = Create(basePay: 100m);

        // Force MinPay > MaxPay via UpdatePay's recompute — validators reject this at write time, so
        // hitting it means a data-integrity bug; the entity throws rather than pay a nonsense amount.
        var bad = OrderEmployeePay.Create(
            orderId: OrderId, employeeId: EmployeeId, payPeriodId: PayPeriodId,
            basePay: 100m, totalPay: 100m, minPay: 200m, maxPay: 150m);

        Assert.Throws<InvalidOperationException>(() => bad.AddBonus(10m));
    }
}

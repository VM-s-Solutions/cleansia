using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.EmployeePayroll.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Currencies;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// Pure-function characterization of
/// <see cref="PayCalculator"/>. This is the money math paid to cleaners. These tests pin the
/// CURRENT behavior of each I/O-free surface with EXACT <c>decimal</c> assertions — no float
/// tolerance. Where a test would reveal a real defect it is logged for a separate fix
/// rather than "fixed" here.
///
/// What is pinned:
///  - CalculateTotalPay formula + floor-at-0 (bonus/deduction edge cases).
///  - CalculateExtrasPay counts only the <c>true</c> extras.
///  - exact decimal results (fractional km × rate, division).
///  - guarded helpers: CalculateDistancePay / ConvertCurrency / ProratePay /
///    SplitPayForMultipleEmployees argument validation.
///  - multi-employee split (sum == total + remainder), proration, currency conversion,
///    and the Aggregate*/*Total roll-ups.
/// </summary>
public class PayCalculatorTests
{
    private static Order OrderWithExtras(Dictionary<string, bool> extras) =>
        Order.Create(
            customerName: "Cust",
            customerEmail: "c@example.com",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("Street 1", "Prague", "10000", "CZ"),
            rooms: 3,
            bathrooms: 2,
            extras: extras,
            cleaningDateTime: new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending);

    // ── CalculateTotalPay: base + extras + expenses + bonus − deduction, floored at 0 ──

    [Fact]
    public void CalculateTotalPay_Sums_All_Components_Minus_Deduction()
    {
        // 400 + 60 + 30 + 50 − 20 = 520
        var total = PayCalculator.CalculateTotalPay(
            basePay: 400m, extrasPay: 60m, expensesPay: 30m, bonusPay: 50m, deductionPay: 20m);

        Assert.Equal(520m, total);
    }

    [Fact]
    public void CalculateTotalPay_With_No_Bonus_Or_Deduction_Returns_Base_Plus_Extras_Plus_Expenses()
    {
        var total = PayCalculator.CalculateTotalPay(
            basePay: 250.50m, extrasPay: 12.25m, expensesPay: 7.30m, bonusPay: 0m, deductionPay: 0m);

        Assert.Equal(270.05m, total);
    }

    [Fact]
    public void CalculateTotalPay_Floors_At_Zero_When_Deduction_Drives_Total_Negative()
    {
        // 100 + 0 + 0 + 0 − 250 = −150 → floored to 0 (cleaner is never paid a negative amount).
        var total = PayCalculator.CalculateTotalPay(
            basePay: 100m, extrasPay: 0m, expensesPay: 0m, bonusPay: 0m, deductionPay: 250m);

        Assert.Equal(0m, total);
    }

    [Fact]
    public void CalculateTotalPay_Returns_Exactly_Zero_When_Deduction_Equals_Total()
    {
        // boundary: net is exactly 0, not negative.
        var total = PayCalculator.CalculateTotalPay(
            basePay: 80m, extrasPay: 10m, expensesPay: 10m, bonusPay: 0m, deductionPay: 100m);

        Assert.Equal(0m, total);
    }

    [Fact]
    public void CalculateTotalPay_Large_Bonus_Adds_On_Top()
    {
        var total = PayCalculator.CalculateTotalPay(
            basePay: 0m, extrasPay: 0m, expensesPay: 0m, bonusPay: 500m, deductionPay: 0m);

        Assert.Equal(500m, total);
    }

    // ── CalculateExtrasPay: count only true-valued extras × rate ──

    [Fact]
    public void CalculateExtrasPay_Counts_Only_True_Extras()
    {
        var order = OrderWithExtras(new Dictionary<string, bool>
        {
            ["windows"] = true,
            ["fridge"] = false,
            ["oven"] = true,
            ["balcony"] = false,
        });

        // 2 true extras × 15 = 30
        var extras = PayCalculator.CalculateExtrasPay(order, extraServiceRate: 15m);

        Assert.Equal(30m, extras);
    }

    [Fact]
    public void CalculateExtrasPay_With_No_True_Extras_Is_Zero()
    {
        var order = OrderWithExtras(new Dictionary<string, bool>
        {
            ["windows"] = false,
            ["fridge"] = false,
        });

        var extras = PayCalculator.CalculateExtrasPay(order, extraServiceRate: 15m);

        Assert.Equal(0m, extras);
    }

    [Fact]
    public void CalculateExtrasPay_With_Empty_Extras_Is_Zero()
    {
        var order = OrderWithExtras(new Dictionary<string, bool>());

        var extras = PayCalculator.CalculateExtrasPay(order, extraServiceRate: 15m);

        Assert.Equal(0m, extras);
    }

    [Fact]
    public void CalculateExtrasPay_Default_Rate_Is_Zero()
    {
        var order = OrderWithExtras(new Dictionary<string, bool> { ["windows"] = true });

        var extras = PayCalculator.CalculateExtrasPay(order);

        Assert.Equal(0m, extras);
    }

    [Fact]
    public void CalculateExtrasPay_Null_Order_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PayCalculator.CalculateExtrasPay(null!, 15m));
    }

    // ── CalculateDistancePay (guards + exact math) ──

    [Fact]
    public void CalculateDistancePay_Multiplies_Distance_By_Rate()
    {
        var pay = PayCalculator.CalculateDistancePay(distance: 12.5m, ratePerKm: 4m);

        Assert.Equal(50.0m, pay);
    }

    [Fact]
    public void CalculateDistancePay_Zero_Distance_Is_Zero()
    {
        Assert.Equal(0m, PayCalculator.CalculateDistancePay(0m, 4m));
    }

    [Fact]
    public void CalculateDistancePay_Negative_Distance_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => PayCalculator.CalculateDistancePay(-1m, 4m));
        Assert.Equal("distance", ex.ParamName);
    }

    [Fact]
    public void CalculateDistancePay_Negative_Rate_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => PayCalculator.CalculateDistancePay(10m, -1m));
        Assert.Equal("ratePerKm", ex.ParamName);
    }

    // ── exact decimal: fractional km × fractional rate, no float drift ──

    [Fact]
    public void CalculateDistancePay_Fractional_Inputs_Are_Exact_Decimal()
    {
        // 7.35 km × 3.20 /km = 23.520 (a double would drift; decimal is exact).
        var pay = PayCalculator.CalculateDistancePay(distance: 7.35m, ratePerKm: 3.20m);

        Assert.Equal(23.5200m, pay);
    }

    // ── ProratePay (guards + math) ──

    [Fact]
    public void ProratePay_Half_Completion_Returns_Half()
    {
        Assert.Equal(50m, PayCalculator.ProratePay(totalPay: 100m, completionPercentage: 50m));
    }

    [Fact]
    public void ProratePay_Full_Completion_Returns_Full()
    {
        Assert.Equal(123.45m, PayCalculator.ProratePay(123.45m, 100m));
    }

    [Fact]
    public void ProratePay_Zero_Completion_Returns_Zero()
    {
        Assert.Equal(0m, PayCalculator.ProratePay(123.45m, 0m));
    }

    [Fact]
    public void ProratePay_Quarter_Completion_Is_Exact_Decimal()
    {
        // 200 × (25/100) = 50.00
        Assert.Equal(50.00m, PayCalculator.ProratePay(200m, 25m));
    }

    [Fact]
    public void ProratePay_Percentage_Below_Zero_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => PayCalculator.ProratePay(100m, -0.01m));
        Assert.Equal("completionPercentage", ex.ParamName);
    }

    [Fact]
    public void ProratePay_Percentage_Above_Hundred_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => PayCalculator.ProratePay(100m, 100.01m));
        Assert.Equal("completionPercentage", ex.ParamName);
    }

    // ── Multi-employee split: sum of splits == total, remainder handling ──

    [Fact]
    public void SplitPayForMultipleEmployees_Even_Split_Is_Exact()
    {
        var share = PayCalculator.SplitPayForMultipleEmployees(totalPay: 300m, employeeCount: 3);

        Assert.Equal(100m, share);
    }

    [Fact]
    public void SplitPayForMultipleEmployees_Single_Employee_Gets_Whole_Total()
    {
        Assert.Equal(250.50m, PayCalculator.SplitPayForMultipleEmployees(250.50m, 1));
    }

    [Fact]
    public void SplitPayForMultipleEmployees_Uneven_Split_Is_A_Pure_Decimal_Divide_No_Remainder_Reconciliation()
    {
        // The method does a plain `totalPay / employeeCount` with NO rounding to cents and NO
        // remainder redistribution. For 50/3 the share is decimal's full-precision quotient and the
        // three shares sum back to 50.000000000000000000000000001 — a sub-cent OVER by 1 unit at the
        // 27th decimal place. This pins the current behavior: remainder reconciliation (if any) is the
        // caller's job, not the calculator's. (See run report — flagged for a rounding-policy follow-up.)
        const decimal total = 50m;
        const int count = 3;

        var share = PayCalculator.SplitPayForMultipleEmployees(total, count);

        // Exact decimal quotient — no rounding to 2 dp.
        Assert.Equal(50m / 3m, share);
        Assert.Equal(16.666666666666666666666666667m, share);

        // Sum of the equal shares is NOT exactly the total — it drifts by a sub-cent epsilon.
        var summed = share * count;
        Assert.NotEqual(total, summed);
        Assert.Equal(50.000000000000000000000000001m, summed);
        Assert.Equal(-0.000000000000000000000000001m, total - summed);
    }

    [Fact]
    public void SplitPayForMultipleEmployees_Three_Way_Of_Hundred_Happens_To_Round_Trip_Exactly()
    {
        // Contrast case: 100/3 is the one where decimal's 28-significant-digit scaling makes the three
        // shares sum back to EXACTLY 100. The round-trip is value-dependent (50/3 above does not), which
        // is itself the reason a real cents-based split policy belongs in the caller, not here.
        var share = PayCalculator.SplitPayForMultipleEmployees(100m, 3);

        Assert.Equal(100m / 3m, share);
        Assert.Equal(100m, share * 3);
    }

    [Fact]
    public void SplitPayForMultipleEmployees_Two_Way_Of_Odd_Cents_Is_Exact_Decimal()
    {
        // 75.50 / 2 = 37.75 — exact, no remainder.
        var share = PayCalculator.SplitPayForMultipleEmployees(75.50m, 2);

        Assert.Equal(37.75m, share);
        Assert.Equal(75.50m, share * 2);
    }

    [Fact]
    public void SplitPayForMultipleEmployees_Zero_Count_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => PayCalculator.SplitPayForMultipleEmployees(100m, 0));
        Assert.Equal("employeeCount", ex.ParamName);
    }

    [Fact]
    public void SplitPayForMultipleEmployees_Negative_Count_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => PayCalculator.SplitPayForMultipleEmployees(100m, -2));
        Assert.Equal("employeeCount", ex.ParamName);
    }

    // ── Currency conversion (guards + same/cross-currency) ──

    [Fact]
    public void ConvertCurrency_Same_Currency_Returns_Amount_Unchanged_Ignoring_Rate()
    {
        var czk = CurrencyMockFactory.Generate(new CurrencyMockFactory.CurrencyPartial { Code = "CZK" });

        // Even with a non-1 rate, same code short-circuits to the original amount.
        var result = PayCalculator.ConvertCurrency(amount: 500m, fromCurrency: czk, toCurrency: czk, exchangeRate: 25m);

        Assert.Equal(500m, result);
    }

    [Fact]
    public void ConvertCurrency_Cross_Currency_Multiplies_By_Rate()
    {
        var czk = CurrencyMockFactory.Generate(new CurrencyMockFactory.CurrencyPartial { Code = "CZK" });
        var eur = CurrencyMockFactory.Generate(new CurrencyMockFactory.CurrencyPartial { Code = "EUR" });

        // 100 EUR × 25.30 = 2530.00 CZK
        var result = PayCalculator.ConvertCurrency(amount: 100m, fromCurrency: eur, toCurrency: czk, exchangeRate: 25.30m);

        Assert.Equal(2530.00m, result);
    }

    [Fact]
    public void ConvertCurrency_Fractional_Rate_Is_Exact_Decimal()
    {
        var czk = CurrencyMockFactory.Generate(new CurrencyMockFactory.CurrencyPartial { Code = "CZK" });
        var usd = CurrencyMockFactory.Generate(new CurrencyMockFactory.CurrencyPartial { Code = "USD" });

        // 49.99 × 22.15 = 1107.2785
        var result = PayCalculator.ConvertCurrency(49.99m, usd, czk, 22.15m);

        Assert.Equal(1107.2785m, result);
    }

    [Fact]
    public void ConvertCurrency_Negative_Amount_Throws()
    {
        var czk = CurrencyMockFactory.Generate(new CurrencyMockFactory.CurrencyPartial { Code = "CZK" });
        var eur = CurrencyMockFactory.Generate(new CurrencyMockFactory.CurrencyPartial { Code = "EUR" });

        var ex = Assert.Throws<ArgumentException>(() => PayCalculator.ConvertCurrency(-1m, eur, czk, 25m));
        Assert.Equal("amount", ex.ParamName);
    }

    [Fact]
    public void ConvertCurrency_NonPositive_Rate_Throws()
    {
        var czk = CurrencyMockFactory.Generate(new CurrencyMockFactory.CurrencyPartial { Code = "CZK" });
        var eur = CurrencyMockFactory.Generate(new CurrencyMockFactory.CurrencyPartial { Code = "EUR" });

        var zero = Assert.Throws<ArgumentException>(() => PayCalculator.ConvertCurrency(100m, eur, czk, 0m));
        Assert.Equal("exchangeRate", zero.ParamName);

        var negative = Assert.Throws<ArgumentException>(() => PayCalculator.ConvertCurrency(100m, eur, czk, -1m));
        Assert.Equal("exchangeRate", negative.ParamName);
    }

    // ── Aggregate roll-ups across many OrderEmployeePay rows ──

    private static OrderEmployeePay Pay(decimal basePay, decimal extras, decimal expenses, decimal bonus, decimal deduction)
    {
        var total = PayCalculator.CalculateTotalPay(basePay, extras, expenses, bonus, deduction);
        return OrderEmployeePay.Create(
            orderId: "ord", employeeId: "emp", payPeriodId: "pp",
            basePay: basePay, extrasPay: extras, expensesPay: expenses,
            bonusPay: bonus, deductionPay: deduction, totalPay: total);
    }

    [Fact]
    public void CalculatePeriodTotal_Sums_TotalPay_Across_Rows()
    {
        var pays = new[]
        {
            Pay(100m, 10m, 5m, 0m, 0m),   // 115
            Pay(200m, 0m, 0m, 20m, 5m),   // 215
            Pay(50m, 0m, 0m, 0m, 0m),     // 50
        };

        Assert.Equal(380m, PayCalculator.CalculatePeriodTotal(pays));
    }

    [Fact]
    public void CalculateDailyTotal_Sums_TotalPay_Across_Rows()
    {
        var pays = new[]
        {
            Pay(120.50m, 0m, 0m, 0m, 0m),
            Pay(79.50m, 0m, 0m, 0m, 0m),
        };

        Assert.Equal(200.00m, PayCalculator.CalculateDailyTotal(pays));
    }

    [Fact]
    public void CalculateDailyTotal_Empty_Is_Zero()
    {
        Assert.Equal(0m, PayCalculator.CalculateDailyTotal(Array.Empty<OrderEmployeePay>()));
    }

    [Fact]
    public void AggregatePeriodBreakdown_Sums_Each_Component_Independently()
    {
        var pays = new[]
        {
            Pay(100m, 10m, 5m, 3m, 1m),
            Pay(200m, 20m, 7m, 4m, 2m),
        };

        var (basePay, extrasPay, expensesPay, bonusPay, deductionPay) =
            PayCalculator.AggregatePeriodBreakdown(pays);

        Assert.Equal(300m, basePay);
        Assert.Equal(30m, extrasPay);
        Assert.Equal(12m, expensesPay);
        Assert.Equal(7m, bonusPay);
        Assert.Equal(3m, deductionPay);
    }

    [Fact]
    public void CountPeriodOrders_Counts_Rows_And_Null_Is_Zero()
    {
        var pays = new[] { Pay(1m, 0m, 0m, 0m, 0m), Pay(2m, 0m, 0m, 0m, 0m) };

        Assert.Equal(2, PayCalculator.CountPeriodOrders(pays));
        Assert.Equal(0, PayCalculator.CountPeriodOrders(null));
    }

    [Fact]
    public void CalculatePeriodTotal_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PayCalculator.CalculatePeriodTotal(null!));
    }

    [Fact]
    public void AggregatePeriodBreakdown_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PayCalculator.AggregatePeriodBreakdown(null!));
    }
}

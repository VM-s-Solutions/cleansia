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
///  - multi-employee split: shares are rounded to the currency minor unit and the last share
///    takes the remainder so the shares sum back to the total EXACTLY; proration; currency
///    conversion; and the Aggregate*/*Total roll-ups.
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

    // ── Multi-employee split: shares are minor-unit-rounded and sum back to the total EXACTLY ──
    //
    // Contract (largest-remainder): the total is divided in whole minor units (2 dp for CZK/EUR); the
    // leftover minor units are handed out one each to the earliest shares. The shares always sum to the
    // input total to the minor unit, and none drifts more than one minor unit from the even share — so
    // no employee is paid a cent short or long.

    [Fact]
    public void SplitPayForMultipleEmployees_Even_Split_Gives_Each_Employee_The_Same_Share()
    {
        var shares = PayCalculator.SplitPayForMultipleEmployees(totalPay: 300m, employeeCount: 3);

        Assert.Equal(new[] { 100m, 100m, 100m }, shares);
        Assert.Equal(300m, shares.Sum());
    }

    [Fact]
    public void SplitPayForMultipleEmployees_Single_Employee_Gets_Whole_Total()
    {
        var shares = PayCalculator.SplitPayForMultipleEmployees(250.50m, 1);

        Assert.Equal(new[] { 250.50m }, shares);
        Assert.Equal(250.50m, shares.Sum());
    }

    [Fact]
    public void SplitPayForMultipleEmployees_Uneven_Split_Rounds_To_Cents_And_Distributes_The_Remainder()
    {
        var shares = PayCalculator.SplitPayForMultipleEmployees(totalPay: 50m, employeeCount: 3);

        Assert.Equal(new[] { 16.67m, 16.67m, 16.66m }, shares);
        Assert.Equal(50m, shares.Sum());
    }

    [Fact]
    public void SplitPayForMultipleEmployees_Three_Way_Of_Hundred_Sums_Back_To_The_Total_Exactly()
    {
        var shares = PayCalculator.SplitPayForMultipleEmployees(100m, 3);

        Assert.Equal(new[] { 33.34m, 33.33m, 33.33m }, shares);
        Assert.Equal(100m, shares.Sum());
    }

    [Fact]
    public void SplitPayForMultipleEmployees_Six_Way_Of_Hundred_Keeps_Every_Share_Within_One_Minor_Unit()
    {
        var shares = PayCalculator.SplitPayForMultipleEmployees(100m, 6);

        Assert.Equal(new[] { 16.67m, 16.67m, 16.67m, 16.67m, 16.66m, 16.66m }, shares);
        Assert.Equal(100m, shares.Sum());
    }

    [Fact]
    public void SplitPayForMultipleEmployees_Two_Way_Of_Odd_Cents_Is_Exact()
    {
        var shares = PayCalculator.SplitPayForMultipleEmployees(75.50m, 2);

        Assert.Equal(new[] { 37.75m, 37.75m }, shares);
        Assert.Equal(75.50m, shares.Sum());
    }

    [Theory]
    [InlineData(0.01, 2)]
    [InlineData(0.01, 3)]
    [InlineData(10.00, 3)]
    [InlineData(10.00, 7)]
    [InlineData(99.99, 4)]
    [InlineData(100.00, 6)]
    [InlineData(123.45, 5)]
    [InlineData(1000.00, 9)]
    [InlineData(0.00, 4)]
    public void SplitPayForMultipleEmployees_Shares_Always_Sum_To_Total_And_Stay_Within_One_Minor_Unit(
        double totalAsDouble, int employeeCount)
    {
        var total = (decimal)totalAsDouble;

        var shares = PayCalculator.SplitPayForMultipleEmployees(total, employeeCount);

        Assert.Equal(employeeCount, shares.Count);
        Assert.Equal(total, shares.Sum());

        var evenShare = Math.Round(total / employeeCount, 2, MidpointRounding.AwayFromZero);
        foreach (var share in shares)
        {
            Assert.True(share >= 0m, $"share {share} was negative");
            Assert.True(Math.Abs(share - evenShare) <= 0.01m,
                $"share {share} drifts more than one minor unit from the even share {evenShare}");
        }
    }

    [Fact]
    public void SplitPayForMultipleEmployees_Exhaustive_Cent_Totals_Always_Reconcile_To_The_Total()
    {
        for (var cents = 0; cents <= 1000; cents++)
        {
            var total = cents / 100m;
            for (var employeeCount = 1; employeeCount <= 9; employeeCount++)
            {
                var shares = PayCalculator.SplitPayForMultipleEmployees(total, employeeCount);

                Assert.Equal(employeeCount, shares.Count);
                Assert.Equal(total, shares.Sum());

                var evenShare = Math.Round(total / employeeCount, 2, MidpointRounding.AwayFromZero);
                Assert.All(shares, share =>
                {
                    Assert.True(share >= 0m);
                    Assert.True(Math.Abs(share - evenShare) <= 0.01m);
                });
            }
        }
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

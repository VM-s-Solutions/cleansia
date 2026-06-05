using Cleansia.Core.Domain.EmployeePayroll;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// Pure-function characterization of
/// <see cref="EmployeePayConfig.CalculatePay(int,int,decimal)"/>. This overload uses its
/// OWN formula and inline min/max clamp, DISTINCT from <c>PayCalculatorExtensions.CalculatePay</c>:
/// here <c>rooms</c> and <c>bathrooms</c> are multiplied DIRECTLY (no "first room is in base"
/// subtraction). The tests pin that exact formula plus the inline clamp.
///
/// What is pinned:
///  - clamp at min (raw total below MinimumPay).
///  - clamp at max (raw total above MaximumPay).
///  - no clamp when Min == 0 and Max == 0 (the <c>&gt; 0</c> guards, not floor/ceiling of 0).
///  - exact decimal arithmetic.
/// Construction is via the factory + <c>SetPayLimits</c> (private setters).
/// </summary>
public class EmployeePayConfigCalculatePayTests
{
    private const string ServiceId = "svc-1";
    private const string CurrencyId = "czk";

    private static EmployeePayConfig Config(
        decimal basePay = 100m,
        decimal extraPerRoom = 0m,
        decimal extraPerBathroom = 0m,
        decimal distanceRatePerKm = 0m,
        decimal minimumPay = 0m,
        decimal maximumPay = 0m)
    {
        var config = EmployeePayConfig.CreateForService(
            serviceId: ServiceId,
            basePay: basePay,
            currencyId: CurrencyId,
            extraPerRoom: extraPerRoom,
            extraPerBathroom: extraPerBathroom,
            distanceRatePerKm: distanceRatePerKm);

        if (minimumPay > 0m || maximumPay > 0m)
        {
            config.SetPayLimits(minimumPay, maximumPay);
        }

        return config;
    }

    // ── Core formula: BasePay + ExtraPerRoom*rooms + ExtraPerBathroom*bathrooms + DistanceRate*distance ──

    [Fact]
    public void CalculatePay_Multiplies_Rooms_And_Bathrooms_Directly()
    {
        // 100 + (10 × 3) + (20 × 2) + (5 × 4) = 100 + 30 + 40 + 20 = 190
        var config = Config(basePay: 100m, extraPerRoom: 10m, extraPerBathroom: 20m, distanceRatePerKm: 5m);

        var pay = config.CalculatePay(rooms: 3, bathrooms: 2, distance: 4m);

        Assert.Equal(190m, pay);
    }

    [Fact]
    public void CalculatePay_With_Zero_Units_Returns_Base_Only()
    {
        var config = Config(basePay: 100m, extraPerRoom: 10m, extraPerBathroom: 20m, distanceRatePerKm: 5m);

        var pay = config.CalculatePay(rooms: 0, bathrooms: 0, distance: 0m);

        Assert.Equal(100m, pay);
    }

    [Fact]
    public void CalculatePay_Fractional_Distance_Is_Exact_Decimal()
    {
        // 100 + (3.30 × 8.5) = 100 + 28.05 = 128.05
        var config = Config(basePay: 100m, distanceRatePerKm: 3.30m);

        var pay = config.CalculatePay(rooms: 0, bathrooms: 0, distance: 8.5m);

        Assert.Equal(128.05m, pay);
    }

    // ── clamp at min ──

    [Fact]
    public void CalculatePay_Clamps_Up_To_MinimumPay_When_Raw_Below_Min()
    {
        // raw = 50, MinimumPay = 120 → 120
        var config = Config(basePay: 50m, minimumPay: 120m, maximumPay: 300m);

        var pay = config.CalculatePay(0, 0, 0m);

        Assert.Equal(120m, pay);
    }

    [Fact]
    public void CalculatePay_At_Exactly_MinimumPay_Is_Not_Clamped()
    {
        // raw = 120 == Min → passes through unchanged (the guard is "< Min", strict).
        var config = Config(basePay: 120m, minimumPay: 120m, maximumPay: 300m);

        var pay = config.CalculatePay(0, 0, 0m);

        Assert.Equal(120m, pay);
    }

    // ── clamp at max ──

    [Fact]
    public void CalculatePay_Clamps_Down_To_MaximumPay_When_Raw_Above_Max()
    {
        // raw = 500, MaximumPay = 300 → 300
        var config = Config(basePay: 500m, minimumPay: 100m, maximumPay: 300m);

        var pay = config.CalculatePay(0, 0, 0m);

        Assert.Equal(300m, pay);
    }

    [Fact]
    public void CalculatePay_At_Exactly_MaximumPay_Is_Not_Clamped()
    {
        // raw = 300 == Max → passes through unchanged (the guard is "> Max", strict).
        var config = Config(basePay: 300m, minimumPay: 100m, maximumPay: 300m);

        var pay = config.CalculatePay(0, 0, 0m);

        Assert.Equal(300m, pay);
    }

    // ── Within-bounds passes through ──

    [Fact]
    public void CalculatePay_Within_Bounds_Passes_Through()
    {
        // raw = 200, bounds [100, 300] → 200
        var config = Config(basePay: 200m, minimumPay: 100m, maximumPay: 300m);

        var pay = config.CalculatePay(0, 0, 0m);

        Assert.Equal(200m, pay);
    }

    // ── no clamp when both limits unset (0 is "unset", not floor/ceiling) ──

    [Fact]
    public void CalculatePay_No_Limits_Set_Passes_Raw_Through_Even_When_Tiny()
    {
        // Min == 0 and Max == 0 → the "> 0" guards are false, so a raw of 1 is NOT floored to 0
        // and a large raw is NOT ceiling'd to 0.
        var config = Config(basePay: 1m, minimumPay: 0m, maximumPay: 0m);

        Assert.Equal(1m, config.CalculatePay(0, 0, 0m));
    }

    [Fact]
    public void CalculatePay_Only_Min_Set_Applies_Floor_But_No_Ceiling()
    {
        // Min = 120, Max = 0 (unset). raw = 5000 → no ceiling, stays 5000; raw below min floors to 120.
        var configHigh = Config(basePay: 5000m, minimumPay: 120m, maximumPay: 0m);
        Assert.Equal(5000m, configHigh.CalculatePay(0, 0, 0m));

        var configLow = Config(basePay: 5m, minimumPay: 120m, maximumPay: 0m);
        Assert.Equal(120m, configLow.CalculatePay(0, 0, 0m));
    }

    [Fact]
    public void CalculatePay_Only_Max_Set_Applies_Ceiling_But_No_Floor()
    {
        // Max = 300, Min = 0 (unset). raw = 5000 → 300; raw = 1 → 1 (no floor).
        var configHigh = Config(basePay: 5000m, minimumPay: 0m, maximumPay: 300m);
        Assert.Equal(300m, configHigh.CalculatePay(0, 0, 0m));

        var configLow = Config(basePay: 1m, minimumPay: 0m, maximumPay: 300m);
        Assert.Equal(1m, configLow.CalculatePay(0, 0, 0m));
    }
}

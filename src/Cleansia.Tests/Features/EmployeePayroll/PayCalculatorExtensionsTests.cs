using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// T-0125 (TC-PAY) — pure-function characterization of
/// <see cref="PayCalculatorExtensions"/> (surface #2): <c>CalculatePay(config, order)</c> and
/// <c>CalculateAggregatedPay(configs, order)</c>, both routed through the private
/// <c>ApplyMinMaxClamp</c>.
///
/// This surface's extras formula DIFFERS from <c>EmployeePayConfig.CalculatePay</c>: here the first
/// room is folded into base — extra rooms = <c>max(0, Rooms − 1)</c> — and expenses are
/// <c>TravelDistance × DistanceRatePerKm</c> with a <c>null</c> distance treated as 0.
///
/// Mapping to acceptance criteria:
///  - AC1 → clamp at min.            - AC2 → clamp at max.
///  - AC3 → no clamp when both unset. - AC4 → Min &gt; Max throws InvalidOperationException.
///  - AC8 → extras use max(0, Rooms−1) and bathrooms; expenses = distance × rate incl. null→0.
///  - AC9 → exact decimal.
/// </summary>
public class PayCalculatorExtensionsTests
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

        // SetPayLimits rejects Max < Min (> 0), so when we need an inconsistent config to drive the
        // AC4 throw we bypass it via reflection (the clamp guard, not the setter, is under test).
        if (minimumPay > 0m && maximumPay > 0m && maximumPay < minimumPay)
        {
            SetLimitsRaw(config, minimumPay, maximumPay);
        }
        else if (minimumPay > 0m || maximumPay > 0m)
        {
            config.SetPayLimits(minimumPay, maximumPay);
        }

        return config;
    }

    private static void SetLimitsRaw(EmployeePayConfig config, decimal min, decimal max)
    {
        typeof(EmployeePayConfig).GetProperty(nameof(EmployeePayConfig.MinimumPay))!
            .SetValue(config, min);
        typeof(EmployeePayConfig).GetProperty(nameof(EmployeePayConfig.MaximumPay))!
            .SetValue(config, max);
    }

    private static Order OrderFor(int rooms, int bathrooms, decimal? travelDistance)
    {
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@example.com",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("Street 1", "Prague", "10000", "CZ"),
            rooms: rooms,
            bathrooms: bathrooms,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending);

        if (travelDistance.HasValue)
        {
            order.SetTravelDistance(travelDistance.Value);
        }

        return order;
    }

    // ── AC8 — extras = max(0, Rooms−1)×perRoom + Bathrooms×perBathroom; expenses = distance×rate ──

    [Fact]
    public void CalculatePay_First_Room_Is_In_Base_Extra_Rooms_Use_Rooms_Minus_One()
    {
        // Rooms = 4 → extra rooms = 3. extras = 3×10 + 2×20 = 70. expenses = 6×5 = 30.
        var config = Config(basePay: 100m, extraPerRoom: 10m, extraPerBathroom: 20m, distanceRatePerKm: 5m);
        var order = OrderFor(rooms: 4, bathrooms: 2, travelDistance: 6m);

        var (basePay, extrasPay, expensesPay, totalPay, _) = config.CalculatePay(order);

        Assert.Equal(100m, basePay);
        Assert.Equal(70m, extrasPay);
        Assert.Equal(30m, expensesPay);
        Assert.Equal(200m, totalPay);
    }

    [Fact]
    public void CalculatePay_Single_Room_Has_Zero_Extra_Rooms()
    {
        // Rooms = 1 → extra rooms = max(0, 0) = 0. extras = 0 + 0 = 0.
        var config = Config(basePay: 100m, extraPerRoom: 10m, extraPerBathroom: 20m);
        var order = OrderFor(rooms: 1, bathrooms: 0, travelDistance: null);

        var (_, extrasPay, _, totalPay, _) = config.CalculatePay(order);

        Assert.Equal(0m, extrasPay);
        Assert.Equal(100m, totalPay);
    }

    [Fact]
    public void CalculatePay_Zero_Rooms_Clamps_Extra_Rooms_At_Zero_Not_Negative()
    {
        // Rooms = 0 → max(0, −1) = 0, NOT −1 (would otherwise subtract pay). Pins the floor.
        var config = Config(basePay: 100m, extraPerRoom: 10m, extraPerBathroom: 20m);
        var order = OrderFor(rooms: 0, bathrooms: 1, travelDistance: null);

        var (_, extrasPay, _, totalPay, _) = config.CalculatePay(order);

        // 0 extra rooms × 10 + 1 bathroom × 20 = 20
        Assert.Equal(20m, extrasPay);
        Assert.Equal(120m, totalPay);
    }

    [Fact]
    public void CalculatePay_Null_Travel_Distance_Is_Treated_As_Zero_Expenses()
    {
        var config = Config(basePay: 100m, distanceRatePerKm: 5m);
        var order = OrderFor(rooms: 1, bathrooms: 0, travelDistance: null);

        var (_, _, expensesPay, totalPay, _) = config.CalculatePay(order);

        Assert.Equal(0m, expensesPay);
        Assert.Equal(100m, totalPay);
    }

    [Fact]
    public void CalculatePay_Fractional_Distance_Expenses_Are_Exact_Decimal()
    {
        // expenses = 7.35 × 3.20 = 23.520
        var config = Config(basePay: 100m, distanceRatePerKm: 3.20m);
        var order = OrderFor(rooms: 1, bathrooms: 0, travelDistance: 7.35m);

        var (_, _, expensesPay, totalPay, _) = config.CalculatePay(order);

        Assert.Equal(23.5200m, expensesPay);
        Assert.Equal(123.5200m, totalPay);
    }

    // ── AC1 — clamp at min ──

    [Fact]
    public void CalculatePay_Clamps_Up_To_Min_When_Raw_Below()
    {
        // raw total = 100, Min = 150 → clamped to 150.
        var config = Config(basePay: 100m, minimumPay: 150m, maximumPay: 400m);
        var order = OrderFor(rooms: 1, bathrooms: 0, travelDistance: null);

        var (_, _, _, totalPay, _) = config.CalculatePay(order);

        Assert.Equal(150m, totalPay);
    }

    // ── AC2 — clamp at max ──

    [Fact]
    public void CalculatePay_Clamps_Down_To_Max_When_Raw_Above()
    {
        // raw total = 500, Max = 400 → clamped to 400.
        var config = Config(basePay: 500m, minimumPay: 100m, maximumPay: 400m);
        var order = OrderFor(rooms: 1, bathrooms: 0, travelDistance: null);

        var (_, _, _, totalPay, _) = config.CalculatePay(order);

        Assert.Equal(400m, totalPay);
    }

    // ── AC3 — no clamp when both unset ──

    [Fact]
    public void CalculatePay_No_Limits_Passes_Raw_Through()
    {
        var config = Config(basePay: 100m, extraPerRoom: 10m, minimumPay: 0m, maximumPay: 0m);
        var order = OrderFor(rooms: 5, bathrooms: 0, travelDistance: null);

        // raw = 100 + 4×10 = 140, no clamp.
        var (_, _, _, totalPay, _) = config.CalculatePay(order);

        Assert.Equal(140m, totalPay);
    }

    // ── AC4 — inconsistent config (Min > Max, both > 0) throws ──

    [Fact]
    public void CalculatePay_Throws_When_Min_Exceeds_Max()
    {
        var config = Config(basePay: 100m, minimumPay: 500m, maximumPay: 200m);
        var order = OrderFor(rooms: 1, bathrooms: 0, travelDistance: null);

        Assert.Throws<InvalidOperationException>(() => config.CalculatePay(order));
    }

    // ── CalculateAggregatedPay — IMP-3 multi-config roll-up ──

    [Fact]
    public void CalculateAggregatedPay_Sums_Base_Extras_Expenses_Across_Configs()
    {
        // Two configs (e.g. a service + a package config) applied to one order.
        var serviceConfig = Config(basePay: 100m, extraPerRoom: 10m, extraPerBathroom: 20m, distanceRatePerKm: 5m);
        var packageConfig = EmployeePayConfig.CreateForPackage(
            packageId: "pkg-1", basePay: 50m, currencyId: CurrencyId,
            extraPerRoom: 5m, extraPerBathroom: 0m, distanceRatePerKm: 2m);

        var order = OrderFor(rooms: 4, bathrooms: 2, travelDistance: 6m); // extra rooms = 3

        var (basePay, extrasPay, expensesPay, totalPay, _) =
            new[] { serviceConfig, packageConfig }.CalculateAggregatedPay(order);

        // base = 100 + 50 = 150
        // extras = (10×3 + 20×2) + (5×3 + 0×2) = 70 + 15 = 85
        // expenses = (5×6) + (2×6) = 30 + 12 = 42
        Assert.Equal(150m, basePay);
        Assert.Equal(85m, extrasPay);
        Assert.Equal(42m, expensesPay);
        Assert.Equal(277m, totalPay);
    }

    [Fact]
    public void CalculateAggregatedPay_Floor_Is_The_Max_Of_Positive_Minimums()
    {
        // raw total = 150 + 0 + 0 = 150. Minimums {0(unset), 200} → floor = 200 → clamp up to 200.
        var c1 = Config(basePay: 150m, minimumPay: 0m, maximumPay: 0m);
        var c2 = Config(basePay: 0m, minimumPay: 200m, maximumPay: 0m);
        var order = OrderFor(rooms: 1, bathrooms: 0, travelDistance: null);

        var (_, _, _, totalPay, _) = new[] { c1, c2 }.CalculateAggregatedPay(order);

        Assert.Equal(200m, totalPay);
    }

    [Fact]
    public void CalculateAggregatedPay_Ceiling_Is_The_Min_Of_Positive_Maximums()
    {
        // raw total = 500 + 500 = 1000. Maximums {600, 800} → ceiling = 600 → clamp down to 600.
        var c1 = Config(basePay: 500m, minimumPay: 0m, maximumPay: 600m);
        var c2 = Config(basePay: 500m, minimumPay: 0m, maximumPay: 800m);
        var order = OrderFor(rooms: 1, bathrooms: 0, travelDistance: null);

        var (_, _, _, totalPay, _) = new[] { c1, c2 }.CalculateAggregatedPay(order);

        Assert.Equal(600m, totalPay);
    }

    [Fact]
    public void CalculateAggregatedPay_No_Limits_Passes_Raw_Through()
    {
        var c1 = Config(basePay: 100m);
        var c2 = Config(basePay: 250m);
        var order = OrderFor(rooms: 1, bathrooms: 0, travelDistance: null);

        var (_, _, _, totalPay, _) = new[] { c1, c2 }.CalculateAggregatedPay(order);

        Assert.Equal(350m, totalPay);
    }
}

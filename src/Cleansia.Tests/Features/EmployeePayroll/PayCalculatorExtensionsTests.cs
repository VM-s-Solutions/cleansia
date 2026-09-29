using System.Globalization;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// Pure-function characterization of
/// <see cref="PayCalculatorExtensions"/>: <c>CalculatePay(config, order)</c> and
/// <c>CalculateAggregatedPay(configs, order)</c>, both routed through the private
/// <c>ApplyMinMaxClamp</c>.
///
/// The first room is folded into base — extra rooms = <c>max(0, Rooms − 1)</c>.
/// Stored distance and kilometre rates no longer contribute to new pay.
///
/// What is pinned:
///  - clamp at min.                  - clamp at max.
///  - no clamp when both unset.       - Min &gt; Max throws InvalidOperationException.
///  - extras use max(0, Rooms−1) and bathrooms; automatic expenses are always zero.
///  - exact decimal.
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
            extraPerBathroom: extraPerBathroom);
        SetLegacyDistanceRate(config, distanceRatePerKm);

        // SetPayLimits rejects Max < Min (> 0), so when we need an inconsistent config to drive the
        // throw we bypass it via reflection (the clamp guard, not the setter, is under test).
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

    private static void SetLegacyDistanceRate(EmployeePayConfig config, decimal rate) =>
        typeof(EmployeePayConfig).GetProperty(nameof(EmployeePayConfig.DistanceRatePerKm))!
            .SetValue(config, rate);

    private static decimal? Distance(string? value) =>
        value is null ? null : decimal.Parse(value, CultureInfo.InvariantCulture);

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
            cleaningDateTime: new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending);

        if (travelDistance.HasValue)
        {
            typeof(Order).GetProperty(nameof(Order.TravelDistance))!
                .SetValue(order, travelDistance.Value);
        }

        return order;
    }

    // ── extras = max(0, Rooms−1)×perRoom + Bathrooms×perBathroom ──

    [Fact]
    public void CalculatePay_First_Room_Is_In_Base_Extra_Rooms_Use_Rooms_Minus_One()
    {
        // Rooms = 4 → extra rooms = 3. extras = 3×10 + 2×20 = 70; legacy distance adds nothing.
        var config = Config(basePay: 100m, extraPerRoom: 10m, extraPerBathroom: 20m, distanceRatePerKm: 5m);
        var order = OrderFor(rooms: 4, bathrooms: 2, travelDistance: 6m);

        var (basePay, extrasPay, expensesPay, totalPay, _) = config.CalculatePay(order);

        Assert.Equal(100m, basePay);
        Assert.Equal(70m, extrasPay);
        Assert.Equal(0m, expensesPay);
        Assert.Equal(170m, totalPay);
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

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("7.35")]
    public void CalculatePay_Ignores_Legacy_Distance_And_Rate(string? distance)
    {
        var config = Config(basePay: 100m, distanceRatePerKm: 5m);
        var order = OrderFor(rooms: 1, bathrooms: 0, travelDistance: Distance(distance));

        var (_, _, expensesPay, totalPay, breakdown) = config.CalculatePay(order);

        Assert.Equal(0m, expensesPay);
        Assert.Equal(100m, totalPay);
        Assert.DoesNotContain("Distance", breakdown);
        Assert.DoesNotContain("km", breakdown);
    }

    [Fact]
    public void CalculatePay_Room_And_Bathroom_Additions_Use_Exact_Decimal()
    {
        var config = Config(basePay: 100m, extraPerRoom: 3.20m, extraPerBathroom: 2.35m, distanceRatePerKm: 9.99m);
        var order = OrderFor(rooms: 4, bathrooms: 2, travelDistance: 7.35m);

        var (_, extrasPay, expensesPay, totalPay, _) = config.CalculatePay(order);

        Assert.Equal(14.30m, extrasPay);
        Assert.Equal(0m, expensesPay);
        Assert.Equal(114.30m, totalPay);
    }

    // ── clamp at min ──

    [Fact]
    public void CalculatePay_Clamps_Up_To_Min_When_Raw_Below()
    {
        // raw total = 100, Min = 150 → clamped to 150.
        var config = Config(basePay: 100m, distanceRatePerKm: 5m, minimumPay: 150m, maximumPay: 400m);
        var order = OrderFor(rooms: 1, bathrooms: 0, travelDistance: 99m);

        var (_, _, _, totalPay, _) = config.CalculatePay(order);

        Assert.Equal(150m, totalPay);
    }

    // ── clamp at max ──

    [Fact]
    public void CalculatePay_Clamps_Down_To_Max_When_Raw_Above()
    {
        // raw total = 500, Max = 400 → clamped to 400.
        var config = Config(basePay: 500m, minimumPay: 100m, maximumPay: 400m);
        var order = OrderFor(rooms: 1, bathrooms: 0, travelDistance: null);

        var (_, _, _, totalPay, _) = config.CalculatePay(order);

        Assert.Equal(400m, totalPay);
    }

    // ── no clamp when both unset ──

    [Fact]
    public void CalculatePay_No_Limits_Passes_Raw_Through()
    {
        var config = Config(basePay: 100m, extraPerRoom: 10m, minimumPay: 0m, maximumPay: 0m);
        var order = OrderFor(rooms: 5, bathrooms: 0, travelDistance: null);

        // raw = 100 + 4×10 = 140, no clamp.
        var (_, _, _, totalPay, _) = config.CalculatePay(order);

        Assert.Equal(140m, totalPay);
    }

    // ── inconsistent config (Min > Max, both > 0) throws ──

    [Fact]
    public void CalculatePay_Throws_When_Min_Exceeds_Max()
    {
        var config = Config(basePay: 100m, minimumPay: 500m, maximumPay: 200m);
        var order = OrderFor(rooms: 1, bathrooms: 0, travelDistance: null);

        Assert.Throws<InvalidOperationException>(() => config.CalculatePay(order));
    }

    // ── CalculateAggregatedPay — multi-config roll-up ──

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("6")]
    public void CalculateAggregatedPay_Sums_Base_And_Extras_Without_Legacy_Distance(string? distance)
    {
        // Two configs (e.g. a service + a package config) applied to one order.
        var serviceConfig = Config(basePay: 100m, extraPerRoom: 10m, extraPerBathroom: 20m, distanceRatePerKm: 5m);
        var packageConfig = EmployeePayConfig.CreateForPackage(
            packageId: "pkg-1", basePay: 50m, currencyId: CurrencyId,
            extraPerRoom: 5m, extraPerBathroom: 0m);
        SetLegacyDistanceRate(packageConfig, 2m);

        var order = OrderFor(rooms: 4, bathrooms: 2, travelDistance: Distance(distance)); // extra rooms = 3
        var configs = new[] { serviceConfig, packageConfig };

        var result = configs.CalculateAggregatedPay(order);
        var (basePay, extrasPay, expensesPay, totalPay, breakdown) = result;

        // base = 100 + 50 = 150
        // extras = (10×3 + 20×2) + (5×3 + 0×2) = 70 + 15 = 85
        Assert.Equal(150m, basePay);
        Assert.Equal(85m, extrasPay);
        Assert.Equal(0m, expensesPay);
        Assert.Equal(235m, totalPay);
        Assert.DoesNotContain("Distance", breakdown);
        Assert.DoesNotContain("Expenses", breakdown);
        Assert.Equal(result, configs.CalculateAggregatedPay(order.Rooms, order.Bathrooms));
    }

    [Fact]
    public void CalculateAggregatedPay_Floor_Is_The_Max_Of_Positive_Minimums()
    {
        // raw total = 150 + 0 + 0 = 150. Minimums {0(unset), 200} → floor = 200 → clamp up to 200.
        var c1 = Config(basePay: 150m, distanceRatePerKm: 5m, minimumPay: 0m, maximumPay: 0m);
        var c2 = Config(basePay: 0m, minimumPay: 200m, maximumPay: 0m);
        var order = OrderFor(rooms: 1, bathrooms: 0, travelDistance: 99m);

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

    // ── CalculateSeatPay — the job split across its seats, dirtiness after the clamp ──

    private static decimal Rate(DirtinessLevel level) => BookingPolicy.DirtinessSurchargeRate(level);

    [Fact]
    public void CalculateSeatPay_One_Seat_Is_The_Whole_Job()
    {
        // 500 + 2 extra rooms x 50 = 600.
        var configs = new[] { Config(basePay: 500m, extraPerRoom: 50m) };

        var seat = configs.CalculateSeatPay(rooms: 3, bathrooms: 0, Rate(DirtinessLevel.Normal), seats: 1, firstSeat: true);

        Assert.Equal(500m, seat.basePay);
        Assert.Equal(100m, seat.extrasPay);
        Assert.Equal(0m, seat.dirtinessPay);
        Assert.Equal(600m, seat.totalPay);
    }

    [Fact]
    public void CalculateSeatPay_Two_Seats_Each_Take_Half_The_Job()
    {
        var configs = new[] { Config(basePay: 500m, extraPerRoom: 50m) };

        var first = configs.CalculateSeatPay(rooms: 3, bathrooms: 0, Rate(DirtinessLevel.Normal), seats: 2, firstSeat: true);
        var second = configs.CalculateSeatPay(rooms: 3, bathrooms: 0, Rate(DirtinessLevel.Normal), seats: 2, firstSeat: false);

        Assert.Equal((250m, 50m, 300m), (first.basePay, first.extrasPay, first.totalPay));
        Assert.Equal((250m, 50m, 300m), (second.basePay, second.extrasPay, second.totalPay));
    }

    [Fact]
    public void CalculateSeatPay_The_Cent_Residue_Goes_To_The_First_Seat()
    {
        // 100.10 over two seats is 50.05 each; increased adds 30.03, which halves to 15.01 and a cent.
        var configs = new[] { Config(basePay: 100.10m) };

        var first = configs.CalculateSeatPay(rooms: 1, bathrooms: 0, Rate(DirtinessLevel.Increased), seats: 2, firstSeat: true);
        var second = configs.CalculateSeatPay(rooms: 1, bathrooms: 0, Rate(DirtinessLevel.Increased), seats: 2, firstSeat: false);

        Assert.Equal(15.02m, first.dirtinessPay);
        Assert.Equal(15.01m, second.dirtinessPay);
        Assert.Equal(65.07m, first.totalPay);
        Assert.Equal(65.06m, second.totalPay);
    }

    [Theory]
    [InlineData(1, DirtinessLevel.Normal)]
    [InlineData(2, DirtinessLevel.Increased)]
    [InlineData(3, DirtinessLevel.Heavy)]
    [InlineData(4, DirtinessLevel.Increased)]
    public void CalculateSeatPay_A_Full_Crew_Adds_Up_To_The_Job(int seats, DirtinessLevel level)
    {
        // Odd cents on purpose: 1000.01 + 3 extra rooms x 33.37 = 1100.12.
        const decimal jobPay = 1100.12m;
        var configs = new[] { Config(basePay: 1000.01m, extraPerRoom: 33.37m) };

        var first = configs.CalculateSeatPay(rooms: 4, bathrooms: 0, Rate(level), seats, firstSeat: true);
        var other = configs.CalculateSeatPay(rooms: 4, bathrooms: 0, Rate(level), seats, firstSeat: false);

        var raisedJobPay = Math.Round(jobPay * (1m + Rate(level)), 2, MidpointRounding.AwayFromZero);
        Assert.Equal(raisedJobPay, first.totalPay + ((seats - 1) * other.totalPay));
        Assert.Equal(raisedJobPay - jobPay, first.dirtinessPay + ((seats - 1) * other.dirtinessPay));
    }

    [Theory]
    [InlineData(DirtinessLevel.Normal, 0, 600)]
    [InlineData(DirtinessLevel.Increased, 180, 780)]
    [InlineData(DirtinessLevel.Heavy, 360, 960)]
    public void CalculateSeatPay_Each_Level_Raises_Pay_By_Its_Rate(DirtinessLevel level, int dirtinessPay, int totalPay)
    {
        var configs = new[] { Config(basePay: 500m, extraPerRoom: 50m) };

        var seat = configs.CalculateSeatPay(rooms: 3, bathrooms: 0, Rate(level), seats: 1, firstSeat: true);

        Assert.Equal((decimal)dirtinessPay, seat.dirtinessPay);
        Assert.Equal((decimal)totalPay, seat.totalPay);
        Assert.Contains($"Dirtiness: {(decimal)dirtinessPay:F2}", seat.breakdown);
    }

    [Fact]
    public void CalculateSeatPay_The_Dirtiness_Term_Is_Added_After_The_Cap()
    {
        // Raw 1000 capped at 800; heavy adds 60 % of the capped 800, so the cap cannot swallow it.
        var configs = new[] { Config(basePay: 1000m, maximumPay: 800m) };

        var seat = configs.CalculateSeatPay(rooms: 1, bathrooms: 0, Rate(DirtinessLevel.Heavy), seats: 1, firstSeat: true);

        Assert.Equal(800m, seat.maxPay);
        Assert.Equal(480m, seat.dirtinessPay);
        Assert.Equal(1280m, seat.totalPay);
    }

    [Fact]
    public void CalculateSeatPay_The_Floor_Is_Split_With_The_Seats_And_Applied_Before_The_Dirtiness_Term()
    {
        // Raw 100 floored to 300 for the job, 150 a seat; increased adds 30 % of that seat's 150.
        var configs = new[] { Config(basePay: 100m, minimumPay: 300m) };

        var seat = configs.CalculateSeatPay(rooms: 1, bathrooms: 0, Rate(DirtinessLevel.Increased), seats: 2, firstSeat: false);

        Assert.Equal(150m, seat.minPay);
        Assert.Equal(45m, seat.dirtinessPay);
        Assert.Equal(195m, seat.totalPay);
    }
}

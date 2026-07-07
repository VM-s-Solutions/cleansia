using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.Domain.Extensions;

public static class PayCalculatorExtensions
{
    public static (decimal basePay, decimal extrasPay, decimal expensesPay, decimal totalPay, string breakdown) CalculatePay(
        this EmployeePayConfig config,
        Order order)
    {
        var basePay = config.BasePay;
        var extraRooms = Math.Max(0, order.Rooms - 1);
        var extrasPay = (extraRooms * config.ExtraPerRoom) + (order.Bathrooms * config.ExtraPerBathroom);
        var expensesPay = (order.TravelDistance ?? 0) * config.DistanceRatePerKm;
        var totalPay = basePay + extrasPay + expensesPay;

        totalPay = ApplyMinMaxClamp(totalPay, config.MinimumPay, config.MaximumPay);

        var breakdown = BuildPayBreakdown(basePay, extraRooms, config.ExtraPerRoom, order.Bathrooms, config.ExtraPerBathroom, order.TravelDistance ?? 0, config.DistanceRatePerKm);

        return (basePay, extrasPay, expensesPay, totalPay, breakdown);
    }

    public static (decimal basePay, decimal extrasPay, decimal expensesPay, decimal totalPay, string breakdown) CalculateAggregatedPay(
        this IEnumerable<EmployeePayConfig> configs,
        Order order) =>
        configs.CalculateAggregatedPay(order.Rooms, order.Bathrooms, order.TravelDistance);

    public static (decimal basePay, decimal extrasPay, decimal expensesPay, decimal totalPay, string breakdown) CalculateAggregatedPay(
        this IEnumerable<EmployeePayConfig> configs,
        int rooms,
        int bathrooms,
        decimal? travelDistance)
    {
        var configList = configs.ToList();

        var basePay = 0m;
        var extrasPay = 0m;
        var expensesPay = 0m;

        var extraRooms = Math.Max(0, rooms - 1);

        foreach (var config in configList)
        {
            basePay += config.BasePay;
            extrasPay += config.ExtraPerRoom * extraRooms;
            extrasPay += config.ExtraPerBathroom * bathrooms;
            expensesPay += config.DistanceRatePerKm * (travelDistance ?? 0m);
        }

        var totalPay = basePay + extrasPay + expensesPay;

        var minimumFloor = configList
            .Where(c => c.MinimumPay > 0)
            .Select(c => c.MinimumPay)
            .DefaultIfEmpty(0m)
            .Max();

        var maximumCeiling = configList
            .Where(c => c.MaximumPay > 0)
            .Select(c => c.MaximumPay)
            .DefaultIfEmpty(0m)
            .Min();

        totalPay = ApplyMinMaxClamp(totalPay, minimumFloor, maximumCeiling);

        var breakdown = $"Base: {basePay:F2}, Extras: {extrasPay:F2}, Expenses: {expensesPay:F2}";

        return (basePay, extrasPay, expensesPay, totalPay, breakdown);
    }

    private static decimal ApplyMinMaxClamp(decimal totalPay, decimal minimumPay, decimal maximumPay)
    {
        if (minimumPay > 0 && maximumPay > 0 && minimumPay > maximumPay)
        {
            throw new InvalidOperationException(
                $"Inconsistent pay config: MinimumPay ({minimumPay}) cannot exceed MaximumPay ({maximumPay}). " +
                "Validators must reject this combination at write time.");
        }

        if (minimumPay > 0 && totalPay < minimumPay)
        {
            totalPay = minimumPay;
        }
        if (maximumPay > 0 && totalPay > maximumPay)
        {
            totalPay = maximumPay;
        }
        return totalPay;
    }

    private static string BuildPayBreakdown(
        decimal basePay,
        int extraRooms,
        decimal perRoom,
        int bathrooms,
        decimal perBathroom,
        decimal distance,
        decimal perKm)
    {
        var parts = new List<string>
        {
            $"Base: {basePay:F2}"
        };

        if (extraRooms > 0 && perRoom > 0)
        {
            parts.Add($"Rooms({extraRooms}): {extraRooms * perRoom:F2}");
        }

        if (bathrooms > 0 && perBathroom > 0)
        {
            parts.Add($"Bathrooms({bathrooms}): {bathrooms * perBathroom:F2}");
        }

        if (distance > 0 && perKm > 0)
        {
            parts.Add($"Distance({distance:F1}km): {distance * perKm:F2}");
        }

        return string.Join(", ", parts);
    }
}

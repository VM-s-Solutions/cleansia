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
        var extrasPay = (order.Rooms * config.ExtraPerRoom) + (order.Bathrooms * config.ExtraPerBathroom);
        var expensesPay = (order.TravelDistance ?? 0) * config.DistanceRatePerKm;
        var totalPay = basePay + extrasPay + expensesPay;

        if (config.MinimumPay > 0 && totalPay < config.MinimumPay)
        {
            totalPay = config.MinimumPay;
        }

        if (config.MaximumPay > 0 && totalPay > config.MaximumPay)
        {
            totalPay = config.MaximumPay;
        }

        var breakdown = BuildPayBreakdown(basePay, order.Rooms, config.ExtraPerRoom, order.Bathrooms, config.ExtraPerBathroom, order.TravelDistance ?? 0, config.DistanceRatePerKm);

        return (basePay, extrasPay, expensesPay, totalPay, breakdown);
    }

    public static (decimal basePay, decimal extrasPay, decimal expensesPay, decimal totalPay, string breakdown) CalculateAggregatedPay(
        this IEnumerable<EmployeePayConfig> configs,
        Order order)
    {
        var configList = configs.ToList();

        var basePay = 0m;
        var extrasPay = 0m;
        var expensesPay = 0m;

        foreach (var config in configList)
        {
            basePay += config.BasePay;

            var extraRooms = Math.Max(0, order.Rooms - 1);
            extrasPay += config.ExtraPerRoom * extraRooms;

            extrasPay += config.ExtraPerBathroom * order.Bathrooms;

            expensesPay += config.DistanceRatePerKm * (order.TravelDistance ?? 0m);
        }

        var totalPay = basePay + extrasPay + expensesPay;

        var primaryConfig = configList.First();
        if (primaryConfig.MinimumPay > 0 && totalPay < primaryConfig.MinimumPay)
        {
            totalPay = primaryConfig.MinimumPay;
        }

        if (primaryConfig.MaximumPay > 0 && totalPay > primaryConfig.MaximumPay)
        {
            totalPay = primaryConfig.MaximumPay;
        }

        var breakdown = $"Base: {basePay:F2}, Extras: {extrasPay:F2}, Expenses: {expensesPay:F2}";

        return (basePay, extrasPay, expensesPay, totalPay, breakdown);
    }

    private static string BuildPayBreakdown(
        decimal basePay,
        int rooms,
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

        if (rooms > 0 && perRoom > 0)
        {
            parts.Add($"Rooms({rooms}): {rooms * perRoom:F2}");
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

using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.EmployeePayroll.Services;

public static class PayCalculator
{
    public static decimal CalculateBasePay(
        EmployeePayConfig payConfig,
        int rooms,
        int bathrooms,
        decimal travelDistance)
    {
        if (payConfig == null)
        {
            throw new ArgumentNullException(nameof(payConfig));
        }

        return payConfig.CalculatePay(rooms, bathrooms, travelDistance);
    }

    public static decimal CalculateExtrasPay(Order order, decimal extraServiceRate = 0)
    {
        if (order == null)
        {
            throw new ArgumentNullException(nameof(order));
        }

        var extrasCount = order.Extras?.Count(e => e.Value) ?? 0;
        return extrasCount * extraServiceRate;
    }

    public static decimal CalculateTotalPay(
        decimal basePay,
        decimal extrasPay,
        decimal expensesPay,
        decimal bonusPay,
        decimal deductionPay)
    {
        var total = basePay + extrasPay + expensesPay + bonusPay - deductionPay;
        return total < 0 ? 0 : total;
    }

    public static decimal CalculateDailyTotal(IEnumerable<OrderEmployeePay> orderPays)
    {
        if (orderPays == null)
        {
            throw new ArgumentNullException(nameof(orderPays));
        }

        return orderPays.Sum(p => p.TotalPay);
    }

    public static decimal CalculatePeriodTotal(IEnumerable<OrderEmployeePay> orderPays)
    {
        if (orderPays == null)
        {
            throw new ArgumentNullException(nameof(orderPays));
        }

        return orderPays.Sum(p => p.TotalPay);
    }

    public static (decimal basePay, decimal extrasPay, decimal expensesPay, decimal bonusPay, decimal deductionPay) AggregatePeriodBreakdown(
        IEnumerable<OrderEmployeePay> orderPays)
    {
        if (orderPays == null)
        {
            throw new ArgumentNullException(nameof(orderPays));
        }

        var paysList = orderPays.ToList();

        return (
            basePay: paysList.Sum(p => p.BasePay),
            extrasPay: paysList.Sum(p => p.ExtrasPay),
            expensesPay: paysList.Sum(p => p.ExpensesPay),
            bonusPay: paysList.Sum(p => p.BonusPay),
            deductionPay: paysList.Sum(p => p.DeductionPay)
        );
    }

    public static string GeneratePeriodSummary(PayPeriod period, IEnumerable<OrderEmployeePay> orderPays)
    {
        if (period == null)
        {
            throw new ArgumentNullException(nameof(period));
        }

        if (orderPays == null)
        {
            throw new ArgumentNullException(nameof(orderPays));
        }

        var paysList = orderPays.ToList();
        var total = CalculatePeriodTotal(paysList);
        var breakdown = AggregatePeriodBreakdown(paysList);

        return $"Period: {period.GetPeriodLabel()} | Orders: {paysList.Count} | " +
               $"Base: {breakdown.basePay:C} | Extras: {breakdown.extrasPay:C} | " +
               $"Expenses: {breakdown.expensesPay:C} | Bonus: {breakdown.bonusPay:C} | " +
               $"Deductions: {breakdown.deductionPay:C} | Total: {total:C}";
    }

    public static int CountPeriodOrders(IEnumerable<OrderEmployeePay> orderPays)
    {
        return orderPays?.Count() ?? 0;
    }

    public static decimal ConvertCurrency(
        decimal amount,
        Currency fromCurrency,
        Currency toCurrency,
        decimal exchangeRate)
    {
        if (amount < 0)
        {
            throw new ArgumentException("Amount cannot be negative", nameof(amount));
        }

        if (exchangeRate <= 0)
        {
            throw new ArgumentException("Exchange rate must be positive", nameof(exchangeRate));
        }

        if (fromCurrency.Code == toCurrency.Code)
        {
            return amount;
        }

        return amount * exchangeRate;
    }

    public static string GenerateInvoiceNumber(Employee employee, PayPeriod period, int sequence = 1)
    {
        if (employee == null)
        {
            throw new ArgumentNullException(nameof(employee));
        }

        if (period == null)
        {
            throw new ArgumentNullException(nameof(period));
        }

        var employeeShort = employee.Id.Substring(0, Math.Min(6, employee.Id.Length)).ToUpper();
        var periodShort = period.Id.Substring(0, Math.Min(6, period.Id.Length)).ToUpper();
        var seqStr = sequence.ToString("D3");

        return $"EMP-{periodShort}-{employeeShort}-{seqStr}";
    }

    public static decimal CalculatePeakTimeBonus(Order order, decimal peakRate)
    {
        if (order == null)
        {
            throw new ArgumentNullException(nameof(order));
        }

        var cleaningTime = order.CleaningDateTime;
        var isPeakTime = IsWeekend(cleaningTime) || IsHoliday(cleaningTime) || IsEveningTime(cleaningTime);

        return isPeakTime ? peakRate : 0;
    }

    public static string GeneratePayBreakdown(
        decimal basePay,
        decimal extrasPay,
        decimal expensesPay,
        decimal bonusPay,
        decimal deductionPay,
        int rooms,
        int bathrooms,
        decimal distance)
    {
        var breakdown = $"Base Pay: {basePay:C}";

        if (rooms > 0)
        {
            breakdown += $" | Rooms: {rooms}";
        }

        if (bathrooms > 0)
        {
            breakdown += $" | Bathrooms: {bathrooms}";
        }

        if (distance > 0)
        {
            breakdown += $" | Distance: {distance:F2} km";
        }

        if (extrasPay > 0)
        {
            breakdown += $" | Extras: {extrasPay:C}";
        }

        if (expensesPay > 0)
        {
            breakdown += $" | Expenses: {expensesPay:C}";
        }

        if (bonusPay > 0)
        {
            breakdown += $" | Bonus: {bonusPay:C}";
        }

        if (deductionPay > 0)
        {
            breakdown += $" | Deductions: -{deductionPay:C}";
        }

        return breakdown;
    }

    public static decimal CalculateDistancePay(decimal distance, decimal ratePerKm)
    {
        if (distance < 0)
        {
            throw new ArgumentException("Distance cannot be negative", nameof(distance));
        }

        if (ratePerKm < 0)
        {
            throw new ArgumentException("Rate per km cannot be negative", nameof(ratePerKm));
        }

        return distance * ratePerKm;
    }

    public static decimal ProratePay(decimal totalPay, decimal completionPercentage)
    {
        if (completionPercentage < 0 || completionPercentage > 100)
        {
            throw new ArgumentException("Completion percentage must be between 0 and 100", nameof(completionPercentage));
        }

        return totalPay * (completionPercentage / 100);
    }

    public static decimal SplitPayForMultipleEmployees(decimal totalPay, int employeeCount)
    {
        if (employeeCount <= 0)
        {
            throw new ArgumentException("Employee count must be positive", nameof(employeeCount));
        }

        return totalPay / employeeCount;
    }

    private static bool IsWeekend(DateTime date)
    {
        return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
    }

    private static bool IsEveningTime(DateTime date)
    {
        return date.Hour >= 18 || date.Hour < 6;
    }

    private static bool IsHoliday(DateTime date)
    {
        // TODO: Implement actual holiday check based on country
        return false;
    }
}

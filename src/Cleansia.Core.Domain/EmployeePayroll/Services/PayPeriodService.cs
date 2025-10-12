namespace Cleansia.Core.Domain.EmployeePayroll.Services;

public static class PayPeriodService
{
    public static PayPeriod GetCurrentPeriod(IEnumerable<PayPeriod> periods, DateOnly date)
    {
        var period = periods.FirstOrDefault(p => p.IsWithinPeriod(date));

        if (period == null)
        {
            throw new InvalidOperationException($"No pay period found for date {date}");
        }

        return period;
    }

    public static PayPeriod GetCurrentPeriod(IEnumerable<PayPeriod> periods, DateTime dateTime)
    {
        return GetCurrentPeriod(periods, DateOnly.FromDateTime(dateTime));
    }

    public static PayPeriod? GetActivePeriod(IEnumerable<PayPeriod> periods)
    {
        return periods
            .Where(p => p.Status == Enums.PayPeriodStatus.Open)
            .OrderByDescending(p => p.EndDate)
            .FirstOrDefault();
    }

    public static IEnumerable<PayPeriod> GenerateBiWeeklyPeriodsForYear(int year, DateOnly? startDate = null)
    {
        var periods = new List<PayPeriod>();
        var current = startDate ?? new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        while (current <= yearEnd)
        {
            var endDate = current.AddDays(13); // 14 days total

            if (endDate > yearEnd)
            {
                endDate = yearEnd;
            }

            periods.Add(PayPeriod.Create(current, endDate));
            current = endDate.AddDays(1);
        }

        return periods;
    }

    public static IEnumerable<PayPeriod> GenerateSemiMonthlyPeriodsForYear(int year)
    {
        var periods = new List<PayPeriod>();

        for (int month = 1; month <= 12; month++)
        {
            periods.Add(PayPeriod.CreateMonthlyFirstHalf(year, month));
            periods.Add(PayPeriod.CreateMonthlySecondHalf(year, month));
        }

        return periods;
    }

    public static bool ShouldClosePeriod(PayPeriod period, DateTime currentDateTime)
    {
        if (period.Status != Enums.PayPeriodStatus.Open)
        {
            return false;
        }

        var currentDate = DateOnly.FromDateTime(currentDateTime);
        return currentDate > period.EndDate;
    }

    public static PayPeriod GetNextPeriod(PayPeriod currentPeriod, int durationDays = 14)
    {
        var nextStart = currentPeriod.EndDate.AddDays(1);
        var nextEnd = nextStart.AddDays(durationDays - 1);

        return PayPeriod.Create(nextStart, nextEnd);
    }

    public static int CalculateWorkingDays(PayPeriod period, IEnumerable<DateOnly>? holidays = null)
    {
        var workingDays = 0;
        var current = period.StartDate;
        var holidaySet = holidays?.ToHashSet() ?? new HashSet<DateOnly>();

        while (current <= period.EndDate)
        {
            var dayOfWeek = current.DayOfWeek;

            if (dayOfWeek != DayOfWeek.Saturday &&
                dayOfWeek != DayOfWeek.Sunday &&
                !holidaySet.Contains(current))
            {
                workingDays++;
            }

            current = current.AddDays(1);
        }

        return workingDays;
    }

    public static bool ValidatePeriod(PayPeriod period, IEnumerable<PayPeriod> existingPeriods)
    {
        if (period.EndDate <= period.StartDate)
        {
            return false;
        }

        return !existingPeriods.Any(existing => existing.Id != period.Id && existing.OverlapsWith(period));
    }

    public static decimal CalculateProratedPay(decimal totalPay, DateOnly hireDate, PayPeriod period)
    {
        if (hireDate <= period.StartDate)
        {
            return totalPay;
        }

        if (hireDate > period.EndDate)
        {
            return 0;
        }

        var totalDays = period.GetPeriodDays();
        var workedDays = period.EndDate.DayNumber - hireDate.DayNumber + 1;

        return totalPay * ((decimal)workedDays / totalDays);
    }
}

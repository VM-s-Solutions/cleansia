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
        var totalPay = basePay + extrasPay;

        totalPay = ApplyMinMaxClamp(totalPay, config.MinimumPay, config.MaximumPay);

        var breakdown = BuildPayBreakdown(basePay, extraRooms, config.ExtraPerRoom, order.Bathrooms, config.ExtraPerBathroom);

        return (basePay, extrasPay, 0m, totalPay, breakdown);
    }

    public static (decimal basePay, decimal extrasPay, decimal expensesPay, decimal totalPay, string breakdown) CalculateAggregatedPay(
        this IEnumerable<EmployeePayConfig> configs,
        Order order) =>
        configs.CalculateAggregatedPay(order.Rooms, order.Bathrooms);

    public static (decimal basePay, decimal extrasPay, decimal expensesPay, decimal totalPay, string breakdown) CalculateAggregatedPay(
        this IEnumerable<EmployeePayConfig> configs,
        int rooms,
        int bathrooms)
    {
        var configList = configs.ToList();

        var basePay = 0m;
        var extrasPay = 0m;

        var extraRooms = Math.Max(0, rooms - 1);

        foreach (var config in configList)
        {
            basePay += config.BasePay;
            extrasPay += config.ExtraPerRoom * extraRooms;
            extrasPay += config.ExtraPerBathroom * bathrooms;
        }

        var totalPay = basePay + extrasPay;

        var (minimumFloor, maximumCeiling) = configList.AggregateBounds();

        totalPay = ApplyMinMaxClamp(totalPay, minimumFloor, maximumCeiling);

        var breakdown = $"Base: {basePay:F2}, Extras: {extrasPay:F2}";

        return (basePay, extrasPay, 0m, totalPay, breakdown);
    }

    /// <summary>
    /// One seat's pay on a job crewed by <paramref name="seats"/> cleaners (owner ruling 2026-09-28): the
    /// rates describe the job, so its base, extras and clamp bounds are split equally across the seats,
    /// the seat's share is clamped, and the dirtiness term - the job's clamped pay x
    /// <paramref name="dirtinessRate"/>, split the same way - is added after the clamp so a cap cannot
    /// swallow it. Every term leaves its cent residue on the first seat, so a full crew's rows add up to
    /// the job. <paramref name="dirtinessRate"/> is <c>BookingPolicy.DirtinessSurchargeRate(level)</c>.
    /// </summary>
    public static (decimal basePay, decimal extrasPay, decimal dirtinessPay, decimal totalPay, decimal minPay, decimal maxPay, string breakdown) CalculateSeatPay(
        this IEnumerable<EmployeePayConfig> configs,
        int rooms,
        int bathrooms,
        decimal dirtinessRate,
        int seats,
        bool firstSeat)
    {
        var configList = configs.ToList();

        var (jobBasePay, jobExtrasPay, _, jobPay, _) = configList.CalculateAggregatedPay(rooms, bathrooms);
        var (jobMinPay, jobMaxPay) = configList.AggregateBounds();
        var jobDirtinessPay = Math.Round(jobPay * dirtinessRate, 2, MidpointRounding.AwayFromZero);

        var basePay = SeatShare(jobBasePay, seats, firstSeat);
        var extrasPay = SeatShare(jobExtrasPay, seats, firstSeat);
        var minPay = SeatShare(jobMinPay, seats, firstSeat);
        var maxPay = SeatShare(jobMaxPay, seats, firstSeat);
        var dirtinessPay = SeatShare(jobDirtinessPay, seats, firstSeat);

        var totalPay = ApplyMinMaxClamp(basePay + extrasPay, minPay, maxPay) + dirtinessPay;

        var breakdown = $"Base: {basePay:F2}, Extras: {extrasPay:F2}, Dirtiness: {dirtinessPay:F2}";

        return (basePay, extrasPay, dirtinessPay, totalPay, minPay, maxPay, breakdown);
    }

    /// <summary>An equal share of <paramref name="amount"/> in whole cents; the first seat also takes the residue.</summary>
    private static decimal SeatShare(decimal amount, int seats, bool firstSeat)
    {
        var share = Math.Floor(amount * 100m / seats) / 100m;
        return firstSeat ? amount - (share * (seats - 1)) : share;
    }

    /// <summary>
    /// The aggregated clamp bounds for a set of pay configs: the floor is the highest positive
    /// MinimumPay (the strongest guarantee wins) and the ceiling is the lowest positive MaximumPay (the
    /// tightest cap wins); 0 on either edge means "no bound" and mirrors the <c>&gt; 0</c> guard in
    /// <see cref="ApplyMinMaxClamp"/>. Exposed so <c>CalculateOrderPay</c> can persist the exact same
    /// bounds on the OrderEmployeePay row it creates, keeping the entity's later re-clamp faithful to
    /// what was applied here (T-0362).
    /// </summary>
    public static (decimal minPay, decimal maxPay) AggregateBounds(this IEnumerable<EmployeePayConfig> configs)
    {
        var configList = configs as IReadOnlyCollection<EmployeePayConfig> ?? configs.ToList();

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

        return (minimumFloor, maximumCeiling);
    }

    public static decimal ApplyMinMaxClamp(decimal totalPay, decimal minimumPay, decimal maximumPay)
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
        decimal perBathroom)
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

        return string.Join(", ", parts);
    }
}

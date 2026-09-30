using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// ADR-0039 D4 — the single definition of "how long is this booking". <c>OrderFactory</c> persists the
/// result as <see cref="Order.EstimatedTime"/>; the preferred-cleaner picker derives the same number to
/// build its overlap window. If the two ever differ, the picker is answering about a different job than
/// the one being booked — which is why <c>ServingCleanersSlotAnswerTests</c> and the span-cap tests pin
/// the in-memory sum and its SQL twins together rather than a comment claiming they match.
///
/// <para>A service included in a selected package and ALSO selected directly is counted twice, and that
/// is the shipped behaviour this function preserves rather than fixes: changing it here would silently
/// re-price every booking's crew size (<c>RequiredEmployees = ceil(EstimatedTime / 120)</c>).</para>
/// </summary>
public static class OrderDuration
{
    /// <summary>
    /// Minutes one cleaner is assumed to cover, the divisor behind
    /// <c>RequiredEmployees = ceil(EstimatedTime / 120)</c>.
    /// </summary>
    /// <remarks>
    /// Named here so the quote and the order divide by the same number rather than
    /// by two copies of 120 that can drift apart.
    /// </remarks>
    public const int MinutesPerEmployee = 120;

    /// <summary>
    /// Each service's own minutes plus its per-room minutes for every room and bathroom, a packaged
    /// service counted the same way, then lengthened by the dirtiness level (owner ruling 2026-09-28).
    /// <paramref name="unitCount"/> is rooms + bathrooms, the count the per-room price multiplies;
    /// <paramref name="dirtinessRate"/> is <c>BookingPolicy.DirtinessSurchargeRate(level)</c>.
    /// </summary>
    public static int EstimateMinutes(
        IEnumerable<Service> services, IEnumerable<Package> packages, int unitCount, decimal dirtinessRate)
        => ScaleForDirtiness(
            services.Sum(s => s.EstimatedTime + s.MinutesPerRoom * unitCount)
            + packages.Sum(p => p.IncludedServices.Sum(i => i.Service!.EstimatedTime + i.Service!.MinutesPerRoom * unitCount)),
            dirtinessRate);

    /// <summary>
    /// minutes x (1 + rate), up to the next whole minute, so a dirtier home is never booked shorter than
    /// the work. The SQL twins sum the catalogue in the database and scale through this.
    /// </summary>
    public static int ScaleForDirtiness(int minutes, decimal dirtinessRate)
        => (int)Math.Ceiling(minutes * (1m + dirtinessRate));

    /// <summary>
    /// The crew the booked minutes need — one cleaner per started <see cref="MinutesPerEmployee"/>,
    /// and never fewer than one, so a selection with no recorded duration still sends somebody.
    /// </summary>
    public static int RequiredEmployees(int estimatedMinutes)
        => estimatedMinutes <= 0 ? 1 : (int)Math.Ceiling(estimatedMinutes / (double)MinutesPerEmployee);
}

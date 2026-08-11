using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// ADR-0039 D4 — the single definition of "how long is this booking". <c>OrderFactory</c> persists the
/// result as <see cref="Order.EstimatedTime"/>; the preferred-cleaner picker derives the same number to
/// build its overlap window. If the two ever differ, the picker is answering about a different job than
/// the one being booked — which is why <c>OrderDurationAgreementTests</c> pins them together rather
/// than a comment claiming they match.
///
/// <para>A service included in a selected package and ALSO selected directly is counted twice, and that
/// is the shipped behaviour this function preserves rather than fixes: changing it here would silently
/// re-price every booking's crew size (<c>RequiredEmployees = ceil(EstimatedTime / 120)</c>).</para>
/// </summary>
public static class OrderDuration
{
    public static int EstimateMinutes(IEnumerable<Service> services, IEnumerable<Package> packages)
        => services.Sum(s => s.EstimatedTime)
         + packages.Sum(p => p.IncludedServices.Sum(i => i.Service!.EstimatedTime));
}

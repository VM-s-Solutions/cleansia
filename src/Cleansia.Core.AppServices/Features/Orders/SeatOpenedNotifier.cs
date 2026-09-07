using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// A seat just came free. Tell the cleaners who could take it, now rather than at the top of the hour.
///
/// <para><b>Why this exists.</b> Until <c>RequestCover</c> and <c>DropOrder</c>, nothing in the
/// platform had ever released a seat, so the only road from a free seat to a cleaner was the hourly
/// digest. A job dropped ninety minutes before its slot reached nobody in time to matter. Owner ruling
/// 2026-09-06: cover has to be found urgently.</para>
///
/// <para><b>The cohort is the digest's, inverted.</b> Same jurisdiction term (the cleaner's work
/// country), same radius preference, same contract gate — read from one place so the two cannot
/// disagree about who is eligible for a job. What it does NOT reuse is the digest's per-cleaner
/// watermark: <c>LastNewJobsDigestAt</c> stays untouched, exactly as the catalogue rules for
/// <c>PreferredOffer</c>, or one targeted nudge would swallow that cleaner's next whole digest.</para>
///
/// <para><b>Best-effort, and it must stay that way.</b> The seat is already released and re-advertised
/// by the time this runs; the push is an accelerant, not the mechanism. A failure here must never
/// fail the release, so the caller commits regardless and the digest remains the floor.</para>
/// </summary>
public static class SeatOpenedNotifier
{
    /// <summary>
    /// A hard ceiling on the fan-out. One release writes one outbox row per recipient, so an
    /// unbounded cohort turns a single tap into an unbounded write — and the cleaners past this
    /// bound still get the job in the next digest. A number rather than "everyone" because nobody has
    /// ruled how a targeted offer should be ordered or rationed, and ADR-0045 D13 refuses a policy
    /// that accretes by accident.
    /// </summary>
    private const int MaxRecipients = 50;

    public static async Task NotifySeatOpenAsync(
        Order order,
        string releasedByEmployeeId,
        string assignmentId,
        IEmployeeRepository employeeRepository,
        INotificationProducer notificationProducer,
        CancellationToken cancellationToken)
    {
        if (order.CustomerAddress?.CountryId is not { } countryId)
        {
            return;
        }

        // Jurisdiction and contract only, in SQL. The radius is a per-cleaner preference evaluated in
        // memory below, exactly as the digest does it.
        var candidates = await employeeRepository
            .GetQueryableIgnoringTenant()
            .Where(e => e.WorkCountryId == countryId
                && e.Id != releasedByEmployeeId
                && (e.ContractStatus == ContractStatus.Approved
                    || e.ContractStatus == ContractStatus.Active)
                && e.UserId != null
                && !order.AssignedEmployees.Any(ae => ae.EmployeeId == e.Id))
            .Select(e => new Candidate(
                e.Id, e.UserId!, e.JobRadiusKm, e.Address!.Latitude, e.Address!.Longitude))
            .Take(MaxRecipients)
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            if (!WithinRadius(candidate, order))
            {
                continue;
            }

            await notificationProducer.NotifyAsync(
                candidate.UserId,
                NotificationEventCatalog.OrderSeatOpen,
                new Dictionary<string, string>
                {
                    ["orderId"] = order.Id,
                    ["orderNumber"] = order.DisplayOrderNumber,
                },
                order.TenantId,
                // The ASSIGNMENT, not the order. One order can open a seat more than once over its
                // life — a cover request answered, then the replacement dropping — and the same
                // cleaner would then mint a key they already hold, which the outbox's unique index
                // raises at commit and which rolls the release back.
                AssignmentNotificationSubject.For(order.Id, assignmentId),
                cancellationToken);
        }
    }

    /// <summary>
    /// "Near me", when the cleaner asked for it. Absent a usable radius or coordinates the cleaner is
    /// included: the work-country term above is the jurisdiction, and a missing preference must widen
    /// rather than silently exclude someone from work.
    /// </summary>
    private static bool WithinRadius(Candidate candidate, Order order)
    {
        if (!JobProximity.Applies(candidate.JobRadiusKm, candidate.Latitude, candidate.Longitude))
        {
            return true;
        }

        // The EXACT test, not the bounding box. The box is a SQL prefilter for a set-shaped query;
        // here there is one order and one cleaner, so the real distance is both cheaper and correct
        // at the east/west extremes the box deliberately over-covers.
        return JobProximity.IsWithinRadius(
            candidate.Latitude!.Value,
            candidate.Longitude!.Value,
            order.CustomerAddress?.Latitude,
            order.CustomerAddress?.Longitude,
            candidate.JobRadiusKm!.Value);
    }

    private sealed record Candidate(
        string EmployeeId, string UserId, int? JobRadiusKm, double? Latitude, double? Longitude);
}

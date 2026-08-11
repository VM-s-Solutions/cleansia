using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

/// <summary>
/// The "new jobs available" digest sweep. Its cadence is the timer's, not this class's — the
/// <c>SendNewJobsDigestCron</c> app-setting (production default: hourly, on the hour).
///
/// Targeting:
///   - <see cref="Domain.Users.Employee.ContractStatus"/> ∈ { Approved, Active }
///   - <see cref="Domain.Users.Employee.WorkCountryId"/> matches the order's
///     customer-address country. This is jurisdiction, not geography, and it is
///     unconditional — the radius below narrows WITHIN it and never replaces it.
///   - The order is within <see cref="Domain.Users.Employee.JobRadiusKm"/> of the cleaner's
///     home address, per <see cref="Domain.Orders.JobProximity"/> — the sentence the partner
///     app has been promising ("jobs near you", "distance from your home"). A cleaner with no
///     radius, or whose home never geocoded, keeps the country-wide board; an order whose own
///     address never geocoded is not counted as near. The rule owns all three fallbacks and
///     explains why the cleaner-side ones fail open while the order-side one fails closed.
///     Two stages: a coarse bounding box in SQL, the exact great-circle test in memory.
///   - The order satisfies <see cref="Domain.Orders.OrderAvailability"/> — the one
///     offerability rule every surface reads — has a free spot, isn't
///     already assigned to this cleaner, and hasn't started yet
///   - The order is not withheld from this cleaner by an ADR-0036 preferred-cleaner hold
///     (<see cref="Domain.Orders.OrderVisibility"/>). This surface carries TWO rules and
///     that is deliberate: "may I take it" is the shared one, "is it new to me" is the
///     local one below. The digest emits a count and never an identity, so nothing can
///     leak here — but a count including an order the cleaner can neither see nor take
///     is a lie.
///   - The order is FRESH to this cleaner (see below)
///   - The cleaner has no overlapping live-commitment order at the order's
///     cleaning time (see <see cref="IOrderRepository.HasOverlappingOrderIgnoringTenantAsync"/>)
///
/// Freshness is THREE sources, disjunctive and upper-bounded at the sweep's own start
/// instant (ADR-0036 §D5.3), because <see cref="Domain.Users.Employee.LastNewJobsDigestAt"/>
/// is a single per-cleaner scalar and two of the filters above are per-cleaner,
/// NON-monotone rules — an order can become takeable again long after its own status
/// stopped changing:
///   - the order's status moved into an offerable state after the watermark; or
///   - one of THIS cleaner's commitments was released (cancelled/completed) after the
///     watermark, and this order sits in the window that release freed. Without the
///     second source, every candidate dropped for a time conflict was burned the moment
///     the cleaner was notified about anything else (T-0528); or
///   - a preferred-cleaner hold on the order EXPIRED after the watermark. A held order's
///     only status track is written at creation, so without the third source its whole
///     history is already older than every other cleaner's watermark by the time it
///     opens, and it leaves the notification channel permanently — board-only, findable
///     solely by someone who happens to scroll.
/// A cleaner who has never been digested has no watermark and no released window: the
/// whole open board is new to them, bounded by the not-started-yet floor.
///
/// Throttling: this method IS the rate-limit — the timer's cadence caps each
/// cleaner to at most one digest per interval. No per-event dedup store is
/// needed because cleaners are only notified about orders that are fresh to
/// them personally.
///
/// Opt-out: each candidate's <see cref="UserNotificationPreferences.NewJobsAvailable"/>
/// gates the enqueue. Cleaners can disable the category and never
/// receive these.
///
/// Tenancy: the sweep runs across all tenants (`GetQueryableIgnoringTenant`)
/// and stamps the per-recipient queue message with the cleaner's
/// <c>TenantId</c> so the downstream consumer scopes correctly.
/// </summary>
public class NewJobsDigestService(
    IEmployeeRepository employeeRepository,
    IOrderRepository orderRepository,
    IUserNotificationPreferencesRepository preferencesRepository,
    INotificationProducer notificationProducer,
    IUnitOfWork unitOfWork,
    ILogger<NewJobsDigestService> logger) : INewJobsDigestService
{
    /// <summary>
    /// The statuses that hand a cleaner's slot back. Exactly the complement of the set
    /// <c>OrderRepository</c>'s overlap predicate treats as slot-blocking — that set is private to the
    /// repository, so <c>NewJobsDigestSkippedJobRecoveryTests</c> is the artifact that goes red if the
    /// two ever stop being complements.
    /// </summary>
    private static readonly OrderStatus[] SlotReleasingStatuses =
    [
        OrderStatus.Completed,
        OrderStatus.Cancelled,
    ];

    public async Task SendDigestsAsync(CancellationToken cancellationToken = default)
    {
        var sweepStartedAt = DateTimeOffset.UtcNow;
        var sweepStartedAtUtc = sweepStartedAt.UtcDateTime;

        // Pull candidate cleaners: approved/active, have a work country
        // set, have a UserId (some legacy rows don't). Include user so we
        // can resolve TenantId for the queue message.
        var cleaners = await employeeRepository
            .GetQueryableIgnoringTenant()
            .Include(e => e.User)
            .Where(e => e.WorkCountryId != null
                && (e.ContractStatus == ContractStatus.Approved
                    || e.ContractStatus == ContractStatus.Active))
            .Select(e => new CleanerCandidate(
                e.Id,
                e.UserId,
                e.WorkCountryId!,
                e.TenantId,
                e.LastNewJobsDigestAt,
                e.JobRadiusKm,
                e.Address!.Latitude,
                e.Address!.Longitude))
            .ToListAsync(cancellationToken);

        if (cleaners.Count == 0)
        {
            logger.LogInformation("NewJobsDigest: no eligible cleaners; nothing to do");
            return;
        }

        var totalEnqueued = 0;
        var totalSkippedNoNewJobs = 0;
        var totalSkippedMuted = 0;
        var totalRadiusUnusable = 0;
        var totalDroppedFarAway = 0;

        foreach (var cleaner in cleaners)
        {
            try
            {
                var sinceUtc = cleaner.LastDigestAt?.UtcDateTime;

                var releasedWindow = sinceUtc is null
                    ? null
                    : await FindReleasedCommitmentWindowAsync(
                        cleaner.EmployeeId, sinceUtc.Value, sweepStartedAt, cancellationToken);

                // Offerable, unfilled orders in this cleaner's work country whose cleaning has not
                // started yet. The floor is what bounds the sweep: without it a cleaner with no
                // watermark matched every offerable order their country ever recorded, and every
                // released window re-opened the same tail.
                var board = orderRepository.GetQueryableIgnoringTenant()
                    .Where(o => o.CustomerAddress != null
                        && o.CustomerAddress.CountryId == cleaner.WorkCountryId
                        && o.CleaningDateTime >= sweepStartedAtUtc
                        && o.AssignedEmployees.Count < o.MaxEmployees
                        && o.AssignedEmployees.All(ae => ae.EmployeeId != cleaner.EmployeeId))
                    .Where(OrderAvailability.IsOfferableSql)
                    .Where(OrderVisibility.NotHeldFrom(cleaner.EmployeeId, sweepStartedAtUtc));

                // "Near you", when the cleaner asked for it. The work-country term above is
                // jurisdiction and stays unconditionally; this narrows within it and never widens.
                var radiusApplies = JobProximity.Applies(
                    cleaner.JobRadiusKm, cleaner.HomeLatitude, cleaner.HomeLongitude);
                if (radiusApplies)
                {
                    board = board.Where(JobProximity.WithinBoundingBoxSql(JobProximity.BoundingBox(
                        cleaner.HomeLatitude!.Value, cleaner.HomeLongitude!.Value, cleaner.JobRadiusKm!.Value)));
                }
                else if (cleaner.JobRadiusKm is not null)
                {
                    // The cleaner asked for a radius and we cannot honour it, because their home never
                    // geocoded. Counting it is the whole point: this fallback is silent by design (they
                    // keep the country-wide board), so nothing else would ever surface the gap.
                    totalRadiusUnusable++;
                }

                // Pull just enough to make a decision: count + the cleaning times the not-busy
                // filter needs, plus the coordinates the exact distance test needs. Bounded by the
                // floor above and by the freshness rule.
                var newOrders = await ApplyFreshness(
                        board, sinceUtc, releasedWindow, cleaner.EmployeeId, sweepStartedAtUtc)
                    .Select(o => new
                    {
                        o.Id,
                        o.CleaningDateTime,
                        o.EstimatedTime,
                        Latitude = o.CustomerAddress!.Latitude,
                        Longitude = o.CustomerAddress!.Longitude,
                    })
                    .ToListAsync(cancellationToken);

                if (newOrders.Count == 0)
                {
                    totalSkippedNoNewJobs++;
                    continue;
                }

                // Not-busy filter: drop orders that overlap one of the cleaner's existing
                // live-commitment orders. Per-order check — the one predicate every surface asks; a
                // dropped order is not lost, it comes back through the released-window source above.
                var takeable = 0;
                foreach (var o in newOrders)
                {
                    // Exact great-circle test over the box's superset, and it runs BEFORE the overlap
                    // check because that one is a database round trip per candidate.
                    if (radiusApplies && !JobProximity.IsWithinRadius(
                        cleaner.HomeLatitude!.Value, cleaner.HomeLongitude!.Value,
                        o.Latitude, o.Longitude, cleaner.JobRadiusKm!.Value))
                    {
                        totalDroppedFarAway++;
                        continue;
                    }

                    var overlaps = await orderRepository.HasOverlappingOrderIgnoringTenantAsync(
                        cleaner.EmployeeId,
                        o.CleaningDateTime,
                        o.EstimatedTime,
                        cancellationToken);
                    if (!overlaps) takeable++;
                }

                if (takeable == 0)
                {
                    totalSkippedNoNewJobs++;
                    continue;
                }

                // Check opt-in. Defaults to true (column default), so a
                // missing prefs row means "allowed" — the dispatch Function
                // has the same defaults-to-true behavior when the row is
                // absent.
                var prefs = await preferencesRepository
                    .GetQueryableIgnoringTenant()
                    .FirstOrDefaultAsync(p => p.UserId == cleaner.UserId, cancellationToken);
                if (prefs != null && !prefs.IsAllowed(NotificationCategory.NewJobsAvailable))
                {
                    totalSkippedMuted++;
                    // Still advance the watermark — otherwise re-enabling
                    // the toggle would burst a backlog of "new" jobs that
                    // are no longer fresh.
                    await StampWatermarkAsync(cleaner.EmployeeId, sweepStartedAt, cancellationToken);
                    continue;
                }

                await notificationProducer.NotifyAsync(
                    cleaner.UserId,
                    NotificationEventCatalog.NewJobsAvailable,
                    new Dictionary<string, string>
                    {
                        ["count"] = takeable.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                    cleaner.TenantId,
                    sweepStartedAt.UtcDateTime.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    cancellationToken);

                // The digest's feed + outbox rows commit together with the cleaner's advanced watermark,
                // so they are durable iff the watermark moved; the drainer puts the push on the wire
                // after the commit.
                await StampWatermarkAsync(cleaner.EmployeeId, sweepStartedAt, cancellationToken);
                totalEnqueued++;
            }
            catch (Exception ex)
            {
                // One cleaner's processing failed — log and continue. We
                // intentionally don't advance the watermark on failure so
                // the next sweep retries them.
                logger.LogWarning(ex,
                    "NewJobsDigest: failed for cleaner {EmployeeId}; continuing",
                    cleaner.EmployeeId);
            }
        }

        logger.LogInformation(
            "NewJobsDigest sweep complete: enqueued={Enqueued} skippedNoNewJobs={NoNew} skippedMuted={Muted} droppedFarAway={FarAway} radiusUnusable={RadiusUnusable} of {Total} cleaners",
            totalEnqueued,
            totalSkippedNoNewJobs,
            totalSkippedMuted,
            totalDroppedFarAway,
            totalRadiusUnusable,
            cleaners.Count);
    }

    /// <summary>
    /// The freshness rule, as three query shapes rather than one expression carrying dead operands.
    ///
    /// <para>The second disjunct is deliberately an OVER-approximation: it re-offers everything in the
    /// merged window a release freed, not just what that release was blocking. Widening freshness can
    /// only cost a redundant candidate — <see cref="IOrderRepository.HasOverlappingOrderIgnoringTenantAsync"/>
    /// still decides takeability — whereas narrowing it loses the job permanently, which is the defect
    /// this exists to close.</para>
    ///
    /// <para>Freshness is `∃ track newer than the watermark`, never `latest track newer than the
    /// watermark`: the two are equivalent, and the existential form drops the correlated top-N subquery
    /// the sweep used to run per candidate row (ADR-0036 §D5.3).</para>
    ///
    /// <para>The hold-expiry disjunct is a bounded WINDOW, `> watermark AND &lt;= sweepStart`, never a
    /// lower bound and never `max(track, hold) > watermark`. The upper bound is the correctness
    /// condition, not an optimisation: <c>PreferredHoldUntilUtc</c> is the first availability instant in
    /// this system that is in the FUTURE, so without it a live hold marks the order "new" from creation
    /// — inflating the count with orders the cleaner cannot see and walking the watermark past the
    /// expiry, which reproduces the very defect the disjunct exists to fix.</para>
    /// </summary>
    private static IQueryable<Order> ApplyFreshness(
        IQueryable<Order> board,
        DateTime? sinceUtc,
        (DateTime Start, DateTime End)? releasedWindow,
        string employeeId,
        DateTime sweepStartedAtUtc)
    {
        if (sinceUtc is null)
        {
            return board;
        }

        var since = sinceUtc.Value;
        if (releasedWindow is null)
        {
            return board.Where(o => o.OrderStatusHistory.Any(s => s.CreatedOn > since)
                || (o.PreferredEmployeeId != null
                    && o.PreferredEmployeeId != employeeId
                    && o.PreferredHoldUntilUtc > since
                    && o.PreferredHoldUntilUtc <= sweepStartedAtUtc));
        }

        var (releasedStart, releasedEnd) = releasedWindow.Value;
        return board.Where(o => o.OrderStatusHistory.Any(s => s.CreatedOn > since)
            || (o.CleaningDateTime < releasedEnd
                && o.CleaningDateTime.AddMinutes(o.EstimatedTime) > releasedStart)
            || (o.PreferredEmployeeId != null
                && o.PreferredEmployeeId != employeeId
                && o.PreferredHoldUntilUtc > since
                && o.PreferredHoldUntilUtc <= sweepStartedAtUtc));
    }

    /// <summary>
    /// The span this cleaner's calendar gave back since their last digest, merged into one interval, or
    /// null if nothing was released. Upper-bounded at the sweep's start so the release is consumed by
    /// exactly one sweep — the watermark lands on that same instant.
    ///
    /// <para>The <see cref="Order.MaxOrderSpanHours"/> floor is the overlap predicate's, for the same
    /// reason and with the same safety argument: a commitment whose own window closed before the sweep
    /// started cannot overlap a candidate, since candidates have not started yet.</para>
    /// </summary>
    private async Task<(DateTime Start, DateTime End)?> FindReleasedCommitmentWindowAsync(
        string employeeId,
        DateTime sinceUtc,
        DateTimeOffset sweepStartedAt,
        CancellationToken cancellationToken)
    {
        var scanFloor = sweepStartedAt.UtcDateTime.AddHours(-Order.MaxOrderSpanHours);

        var released = await orderRepository.GetQueryableIgnoringTenant()
            .Where(o => o.AssignedEmployees.Any(ae => ae.EmployeeId == employeeId)
                && o.CleaningDateTime >= scanFloor
                && o.OrderStatusHistory.Any(s => SlotReleasingStatuses.Contains(s.Status)
                    && s.CreatedOn > sinceUtc
                    && s.CreatedOn <= sweepStartedAt))
            .Select(o => new { o.CleaningDateTime, o.EstimatedTime })
            .ToListAsync(cancellationToken);

        if (released.Count == 0)
        {
            return null;
        }

        return (released.Min(r => r.CleaningDateTime),
                released.Max(r => r.CleaningDateTime.AddMinutes(r.EstimatedTime)));
    }

    /// <summary>
    /// Bump the cleaner's watermark to the sweep-start timestamp and
    /// commit. Doing the write inside the per-cleaner loop (not at the
    /// end) means a mid-sweep crash won't re-notify already-handled
    /// cleaners. Uses sweep-start (not now()) so orders that became
    /// available DURING the sweep are picked up by the next run.
    ///
    /// Reads tenant-IGNORING for the same reason the candidate query does — the
    /// timer runs with no tenant context, so a tenant-scoped read resolves no
    /// tenanted cleaner and their watermark could never advance, re-notifying
    /// them about the same jobs every sweep. The entity stays change-tracked,
    /// so the stamp still commits together with the feed + outbox rows.
    /// </summary>
    private async Task StampWatermarkAsync(
        string employeeId,
        DateTimeOffset stamp,
        CancellationToken cancellationToken)
    {
        var employee = await employeeRepository.GetByIdIgnoringTenantAsync(employeeId, cancellationToken);
        if (employee is null)
        {
            logger.LogWarning(
                "NewJobsDigest: cleaner {EmployeeId} disappeared between the sweep read and the watermark stamp; watermark not advanced",
                employeeId);
            return;
        }

        employee.MarkNewJobsDigestSent(stamp);
        await unitOfWork.CommitAsync(cancellationToken);
    }

    private sealed record CleanerCandidate(
        string EmployeeId,
        string UserId,
        string WorkCountryId,
        string? TenantId,
        DateTimeOffset? LastDigestAt,
        int? JobRadiusKm,
        double? HomeLatitude,
        double? HomeLongitude);
}

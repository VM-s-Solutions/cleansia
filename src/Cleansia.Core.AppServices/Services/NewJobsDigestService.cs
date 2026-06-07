using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

/// <summary>
/// 30-min "new jobs available" digest sweep.
///
/// Targeting (v1):
///   - <see cref="Domain.Users.Employee.ContractStatus"/> ∈ { Approved, Active }
///   - <see cref="Domain.Users.Employee.WorkCountryId"/> matches the order's
///     customer-address country
///   - The order is in the "available" status set (Pending, Confirmed),
///     has a free spot, and isn't already assigned to this cleaner
///   - The order's status flipped to one of the available states AFTER the
///     cleaner's last digest watermark (so old already-notified orders
///     don't re-trigger)
///   - The cleaner has no overlapping in-progress order at the order's
///     cleaning time (see <see cref="IOrderRepository.HasOverlappingOrderAsync"/>)
///
/// Throttling: this method IS the rate-limit — the timer's 30-min cadence
/// caps each cleaner to at most one digest per interval. No per-event
/// dedup store is needed because cleaners are only notified about orders
/// newer than their personal watermark.
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
    IPendingDispatch pendingDispatch,
    IUnitOfWork unitOfWork,
    ILogger<NewJobsDigestService> logger) : INewJobsDigestService
{
    /// <summary>
    /// Status set considered "available" for a cleaner to take. Mirrors
    /// <c>DashboardSpecifications.CreateAvailableOrdersSpec</c>.
    /// </summary>
    private static readonly OrderStatus[] AvailableStatuses =
        { OrderStatus.New, OrderStatus.Pending, OrderStatus.Confirmed };

    public async Task SendDigestsAsync(CancellationToken cancellationToken = default)
    {
        var sweepStartedAt = DateTimeOffset.UtcNow;

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
                e.LastNewJobsDigestAt))
            .ToListAsync(cancellationToken);

        if (cleaners.Count == 0)
        {
            logger.LogInformation("NewJobsDigest: no eligible cleaners; nothing to do");
            return;
        }

        var totalEnqueued = 0;
        var totalSkippedNoNewJobs = 0;
        var totalSkippedMuted = 0;

        foreach (var cleaner in cleaners)
        {
            try
            {
                var sinceUtc = (cleaner.LastDigestAt ?? DateTimeOffset.MinValue).UtcDateTime;

                // Available orders in this cleaner's work country that became
                // available AFTER their last digest. "Became available" is
                // proxied by the latest OrderStatusTrack's CreatedOn for a
                // status in AvailableStatuses — i.e. the transition into a
                // takeable state. This avoids re-notifying about orders that
                // sat in Pending/Confirmed across many sweeps.
                var newJobsQuery = orderRepository.GetQueryableIgnoringTenant()
                    .Include(o => o.CustomerAddress)
                    .Include(o => o.AssignedEmployees)
                    .Include(o => o.OrderStatusHistory)
                    .Where(o => o.CustomerAddress != null
                        && o.CustomerAddress.CountryId == cleaner.WorkCountryId
                        && o.AssignedEmployees.Count < o.MaxEmployees
                        && o.AssignedEmployees.All(ae => ae.EmployeeId != cleaner.EmployeeId))
                    // Latest status track must be in the available set AND
                    // newer than the watermark. EF translates this into a
                    // correlated subquery; it's the same shape the
                    // available-orders spec already uses.
                    .Where(o => o.OrderStatusHistory
                        .OrderByDescending(s => s.CreatedOn)
                        .Take(1)
                        .Any(s => AvailableStatuses.Contains(s.Status)
                            && s.CreatedOn > sinceUtc));

                // Pull just enough to make a decision: count + min/max
                // cleaning times we'd need for the not-busy filter. Pulling
                // (id, cleaningDateTime, estimatedTimeMinutes) keeps the
                // per-cleaner page tiny.
                var newOrders = await newJobsQuery
                    .Select(o => new { o.Id, o.CleaningDateTime, o.EstimatedTime })
                    .ToListAsync(cancellationToken);

                if (newOrders.Count == 0)
                {
                    totalSkippedNoNewJobs++;
                    continue;
                }

                // Not-busy filter: drop orders that overlap one of the
                // cleaner's existing in-progress orders. Per-order check —
                // expensive in the worst case but bounded by how many new
                // orders matched the country filter for THIS cleaner.
                var takeable = 0;
                foreach (var o in newOrders)
                {
                    var overlaps = await orderRepository.HasOverlappingOrderAsync(
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

                var messageKey = MessageKeys.Push(
                    cleaner.UserId, NotificationEventCatalog.NewJobsAvailable, sweepStartedAt.UtcDateTime.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
                pendingDispatch.Enqueue(
                    QueueNames.NotificationsDispatch,
                    new QueueEnvelope<SendPushNotificationMessage>(
                        messageKey,
                        cleaner.TenantId,
                        new SendPushNotificationMessage(
                            UserId: cleaner.UserId,
                            EventKey: NotificationEventCatalog.NewJobsAvailable,
                            Args: new Dictionary<string, string>
                            {
                                ["count"] = takeable.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            },
                            TenantId: cleaner.TenantId)),
                    messageKey);

                // The digest's outbox row commits together with the cleaner's advanced watermark, so the
                // row is durable iff the watermark moved; the drainer puts it on the wire after the commit.
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
            "NewJobsDigest sweep complete: enqueued={Enqueued} skippedNoNewJobs={NoNew} skippedMuted={Muted} of {Total} cleaners",
            totalEnqueued,
            totalSkippedNoNewJobs,
            totalSkippedMuted,
            cleaners.Count);
    }

    /// <summary>
    /// Bump the cleaner's watermark to the sweep-start timestamp and
    /// commit. Doing the write inside the per-cleaner loop (not at the
    /// end) means a mid-sweep crash won't re-notify already-handled
    /// cleaners. Uses sweep-start (not now()) so orders that became
    /// available DURING the sweep are picked up by the next run.
    /// </summary>
    private async Task StampWatermarkAsync(
        string employeeId,
        DateTimeOffset stamp,
        CancellationToken cancellationToken)
    {
        var employee = await employeeRepository.GetByIdAsync(employeeId, cancellationToken);
        if (employee is null) return;
        employee.MarkNewJobsDigestSent(stamp);
        await unitOfWork.CommitAsync(cancellationToken);
    }

    private sealed record CleanerCandidate(
        string EmployeeId,
        string UserId,
        string WorkCountryId,
        string? TenantId,
        DateTimeOffset? LastDigestAt);
}

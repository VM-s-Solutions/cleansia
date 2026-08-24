using System.Globalization;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Specifications;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The evening digest: how many jobs a cleaner has tomorrow, sent at 18:00 in THEIR local time.
///
/// <para><b>Why an hourly sweep and not a daily one.</b> An Azure timer fires on a UTC cron and cannot
/// be timezone-aware, so the sweep runs every hour and picks out the cleaners whose local clock has
/// just struck <see cref="Command.LocalSendHour"/>. Every seeded market is CET today, which makes this
/// look like needless machinery — it stops looking that way the first time a non-CET country is
/// added, and by then the digest would already have been arriving at the wrong hour for months.</para>
///
/// <para><b>The zone comes from the cleaner's work country, never from a client.</b>
/// <c>Employee.WorkCountryId</c> to <c>CountryConfiguration.TimeZoneId</c>, resolved through
/// <see cref="TimeZoneResolution"/>, which never throws and falls to UTC on an unknown id. The
/// <c>X-Time-Zone</c> header is unauthenticated and is banned on any path that decides something.</para>
///
/// <para><b>Its own watermark, not the new-jobs one.</b> Two writers on one scalar suppress each
/// other, which is exactly why the preferred-offer notification refuses to stamp
/// <c>LastNewJobsDigestAt</c>. This answers "have I told them about tomorrow", a different question
/// from "what is new on the board".</para>
///
/// <para>Non-mutable, like the two per-job reminders — a cleaner who can silence their own schedule
/// can silence the thing that stops them forgetting it.</para>
/// </summary>
public class SendTomorrowJobDigest
{
    /// <param name="LocalSendHour">The hour, in the cleaner's own timezone, the digest is sent at.</param>
    /// <param name="CatchUpHours">
    /// How many local hours after <paramref name="LocalSendHour"/> a digest may still be sent.
    ///
    /// <para><b>This is why the hour test is a window and not an equality.</b> An equality gives the
    /// whole timezone exactly ONE attempt per day: a cleaner who takes tomorrow's job at 18:30 is never
    /// told, because at 19:00 the predicate is already false — no failure needed, that is simply the
    /// shipped behaviour. Any tick that throws before its group commits loses that evening outright.
    /// A window lets the next tick finish the job, and the per-local-date watermark keeps it to one
    /// message.</para>
    ///
    /// <para><b>Bounded, not open-ended.</b> A bare <c>&gt;=</c> would keep sending until local
    /// midnight, so a cleaner who took a job at 22:50 gets an unsilenceable push at 23:00 — these keys
    /// are non-mutable and the platform has no quiet hours. Three hours ends the window at 21:00, which
    /// is still an evening.</para>
    /// </param>
    public record Command(int LocalSendHour = 18, int CatchUpHours = 3) : ICommand<Response>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.LocalSendHour).InclusiveBetween(0, 23);
            RuleFor(x => x.CatchUpHours).InclusiveBetween(1, 6);
        }
    }

    public record Response(int DigestsSent, int CleanersConsidered);

    public class Handler(
        IEmployeeRepository employeeRepository,
        IOrderRepository orderRepository,
        ICountryConfigurationRepository countryConfigurationRepository,
        INotificationProducer notificationProducer,
        ITenantProvider tenantProvider,
        IUnitOfWork unitOfWork,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;

            // A work country is what makes a timezone resolvable at all; JobReminderRecipient answers
            // who may be told about work, once, for all three reminder keys.
            //
            // TRACKED, and deliberately not projected. The stamp below writes to this same instance, so
            // the digest and its watermark are one unit of work: a projection forced a second read to
            // find the entity, and a null from that read enqueued the outbox row with no watermark
            // behind it — which the catch-up window would then replay onto an identical message key.
            var cleaners = await employeeRepository
                .GetQueryableIgnoringTenant()
                .Include(e => e.User)
                .Where(e => e.WorkCountryId != null)
                .Where(JobReminderRecipient.Predicate)
                .ToListAsync(cancellationToken);

            if (cleaners.Count == 0)
            {
                return BusinessResult.Success(new Response(0, 0));
            }

            // One lookup per distinct work country, not per cleaner — the sweep walks every approved
            // cleaner on the platform and the same handful of countries would otherwise be re-read
            // hundreds of times.
            var zones = new Dictionary<string, TimeZoneInfo>(StringComparer.Ordinal);
            foreach (var countryId in cleaners.Select(c => c.WorkCountryId!).Distinct(StringComparer.Ordinal))
            {
                var configuration = await countryConfigurationRepository
                    .GetByCountryIdAsync(countryId, cancellationToken);
                zones[countryId] = TimeZoneResolution.Resolve(configuration?.TimeZoneId);
            }

            // ADR-0054 required change 6, discharged. The per-cleaner CountAsync below used to run once
            // for EVERY approved cleaner on the platform, every evening, to send at most a handful of
            // messages. Eligibility is decided first — in memory, from the clock and the watermark, no
            // query — and only the cleaners who survive it are counted, in ONE query per distinct local
            // day rather than one per person.
            //
            // Safe to run before the tenant loop and outside it: this reads through
            // GetQueryableIgnoringTenant and writes nothing. Every write — the outbox row and the
            // watermark — stays inside the loop with its own commit, because rows are stamped from the
            // ambient tenant AT COMMIT TIME and one deferred commit would stamp every group with the
            // last tenant seen.
            var eligible = EligibleThisTick(cleaners, zones, nowUtc, command);
            var counts = await CountTomorrowsJobsAsync(eligible, cancellationToken);

            var sent = 0;
            foreach (var tenantGroup in cleaners.GroupBy(c => c.TenantId ?? string.Empty))
            {
                tenantProvider.ClearTenantOverride();
                if (!string.IsNullOrEmpty(tenantGroup.Key))
                {
                    tenantProvider.SetTenantOverride(tenantGroup.Key);
                }

                foreach (var cleaner in tenantGroup)
                {
                    try
                    {
                        if (await TrySendAsync(cleaner, eligible, counts, nowUtc, cancellationToken))
                        {
                            sent++;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex,
                            "SendTomorrowJobDigest failed for cleaner {EmployeeId}; continuing",
                            cleaner.Id);
                    }
                }

                await unitOfWork.CommitAsync(cancellationToken);
            }

            if (sent > 0)
            {
                logger.LogInformation(
                    "SendTomorrowJobDigest sent {Sent} digests of {Total} cleaners considered",
                    sent, cleaners.Count);
            }

            return BusinessResult.Success(new Response(sent, cleaners.Count));
        }

        /// <summary>
        /// Who this tick may send to at all, decided from the clock and the watermark alone.
        ///
        /// <para>Split out so the decision costs NO query. It used to be interleaved with the count, so
        /// every approved cleaner on the platform paid a <c>CountAsync</c> before the hour gate could
        /// turn them away — and the hour gate turns away all but one timezone's worth of them.</para>
        /// </summary>
        private static List<EligibleCleaner> EligibleThisTick(
            IReadOnlyList<Employee> cleaners,
            IReadOnlyDictionary<string, TimeZoneInfo> zones,
            DateTime nowUtc,
            Command command)
        {
            var eligible = new List<EligibleCleaner>();
            foreach (var cleaner in cleaners)
            {
                var zone = zones[cleaner.WorkCountryId!];
                var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone);

                // The send WINDOW, not the send hour. See Command.CatchUpHours for why an equality
                // here gives a whole timezone one attempt a day. The window does not wrap past
                // midnight: a LocalSendHour late enough for LocalSendHour + CatchUpHours to exceed 23
                // simply ends at 23:00, which is the correct place to stop pushing anyway.
                if (localNow.Hour < command.LocalSendHour
                    || localNow.Hour >= command.LocalSendHour + command.CatchUpHours)
                {
                    continue;
                }

                // Already told them about this local day. Compared in the cleaner's own zone, not UTC:
                // around midnight the two disagree about which day it is, and a UTC comparison would
                // send a second digest for the same evening to anyone east of Greenwich.
                var localToday = DateOnly.FromDateTime(localNow);
                if (cleaner.LastTomorrowDigestAt is { } last
                    && DateOnly.FromDateTime(
                        TimeZoneInfo.ConvertTimeFromUtc(last.UtcDateTime, zone)) == localToday)
                {
                    continue;
                }

                // Tomorrow, bounded in the cleaner's local day and converted back to UTC for the
                // query — the column is UTC and a local-time comparison against it would be off by
                // the offset.
                var localTomorrowStart = localNow.Date.AddDays(1);
                eligible.Add(new EligibleCleaner(
                    cleaner,
                    localToday,
                    TimeZoneInfo.ConvertTimeToUtc(localTomorrowStart, zone),
                    TimeZoneInfo.ConvertTimeToUtc(localTomorrowStart.AddDays(1), zone)));
            }

            return eligible;
        }

        /// <summary>
        /// How many jobs each eligible cleaner has tomorrow — ONE query per distinct local day rather
        /// than one per cleaner. ADR-0054 required change 6.
        ///
        /// <para>Grouped by the WINDOW rather than by the country, because the window is the only
        /// thing the query cares about: two countries on the same offset share a local tomorrow and
        /// therefore share a query. A cleaner absent from the result has no jobs, which is the
        /// ordinary case and deliberately costs nothing to represent.</para>
        /// </summary>
        private async Task<Dictionary<string, int>> CountTomorrowsJobsAsync(
            IReadOnlyList<EligibleCleaner> eligible,
            CancellationToken cancellationToken)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (eligible.Count == 0)
            {
                return counts;
            }

            foreach (var window in eligible.GroupBy(e => (e.WindowStartUtc, e.WindowEndUtc)))
            {
                var ids = window.Select(e => e.Cleaner.Id).Distinct(StringComparer.Ordinal).ToList();
                var start = window.Key.WindowStartUtc;
                var end = window.Key.WindowEndUtc;

                var rows = await orderRepository.GetQueryableIgnoringTenant()
                    .Where(o => o.CurrentStatus == OrderStatus.Confirmed
                        && o.CleaningDateTime >= start
                        && o.CleaningDateTime < end)
                    .SelectMany(o => o.AssignedEmployees)
                    .Where(ae => ids.Contains(ae.EmployeeId))
                    .GroupBy(ae => ae.EmployeeId)
                    .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
                    .ToListAsync(cancellationToken);

                foreach (var row in rows)
                {
                    counts[row.EmployeeId] = row.Count;
                }
            }

            return counts;
        }

        private async Task<bool> TrySendAsync(
            Employee cleaner,
            IReadOnlyList<EligibleCleaner> eligible,
            IReadOnlyDictionary<string, int> counts,
            DateTime nowUtc,
            CancellationToken cancellationToken)
        {
            var entry = eligible.FirstOrDefault(e => ReferenceEquals(e.Cleaner, cleaner));
            if (entry is null)
            {
                return false;
            }

            // No jobs tomorrow is not news, and a digest saying "0" would train cleaners to ignore the
            // one that says 2. The watermark is deliberately NOT stamped either, so a job taken later
            // this evening still produces a digest on a later tick — which is only true because the
            // hour test is a window. Under the equality it replaced, there was no later tick and the
            // recovery it advertises could never happen.
            if (!counts.TryGetValue(cleaner.Id, out var count) || count == 0)
            {
                return false;
            }

            await notificationProducer.NotifyAsync(
                cleaner.UserId,
                NotificationEventCatalog.ReminderTomorrow,
                new Dictionary<string, string>
                {
                    ["count"] = count.ToString(CultureInfo.InvariantCulture),
                },
                cleaner.TenantId,
                // The local date being summarised — deterministic, so a re-run of the same evening
                // dedups onto one key rather than minting a fresh message per tick.
                entry.LocalToday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                cancellationToken);

            // The tracked instance the candidate query returned — no re-read, and no null to swallow.
            cleaner.MarkTomorrowDigestSent(new DateTimeOffset(nowUtc, TimeSpan.Zero));

            return true;
        }

        /// <summary>A cleaner the clock and the watermark admit, with the local day they are owed.</summary>
        private sealed record EligibleCleaner(
            Employee Cleaner,
            DateOnly LocalToday,
            DateTime WindowStartUtc,
            DateTime WindowEndUtc);
    }
}

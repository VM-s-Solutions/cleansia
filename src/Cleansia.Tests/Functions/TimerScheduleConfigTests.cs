using System.Reflection;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Functions.Functions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Tests.Functions;

// The tokenized timers must read their cron from an app-setting via
// %AppSetting% TimerTrigger syntax (promotion is config-only), the committed production defaults must
// match each function's documented cadence, and Materialize must fire strictly before Reminder.
public class TimerScheduleConfigTests
{
    private const string MaterializeToken = "%MaterializeRecurringBookingsCron%";
    private const string RemindersToken = "%SendRecurringOrderRemindersCron%";
    private const string MembershipToken = "%SendMembershipLifecycleNotificationsCron%";
    private const string DigestToken = "%SendNewJobsDigestCron%";
    private const string ExpireReferralsToken = "%ExpireStaleReferralsCron%";
    private const string PreCleaningToken = "%SendPreCleaningRemindersCron%";
    private const string CleanerJobRemindersToken = "%SendCleanerJobRemindersCron%";
    private const string TomorrowDigestToken = "%SendTomorrowJobDigestCron%";

    private const string MaterializeCron = "0 0 2 * * *";
    private const string RemindersCron = "0 30 2 * * *";
    private const string MembershipCron = "0 0 3 * * *";
    private const string DigestCron = "0 0 * * * *";
    private const string ExpireReferralsCron = "0 30 3 * * *";
    private const string PreCleaningCron = "0 */5 * * * *";
    private const string CleanerJobRemindersCron = "0 */5 * * * *";
    private const string TomorrowDigestCron = "0 0 * * * *";

    private static readonly IConfiguration ProductionDefaults = BuildProductionDefaults();

    [Theory]
    [InlineData(typeof(MaterializeRecurringBookingsFunction), MaterializeToken)]
    [InlineData(typeof(SendRecurringOrderRemindersFunction), RemindersToken)]
    [InlineData(typeof(SendMembershipLifecycleNotificationsFunction), MembershipToken)]
    [InlineData(typeof(SendNewJobsDigestTimerFunction), DigestToken)]
    [InlineData(typeof(ExpireStaleReferralsFunction), ExpireReferralsToken)]
    [InlineData(typeof(SendPreCleaningRemindersFunction), PreCleaningToken)]
    [InlineData(typeof(SendCleanerJobRemindersFunction), CleanerJobRemindersToken)]
    [InlineData(typeof(SendTomorrowJobDigestFunction), TomorrowDigestToken)]
    public void Trigger_reads_cron_from_app_setting_token(Type functionType, string expectedToken)
    {
        var schedule = ReadSchedule(functionType);

        Assert.Equal(expectedToken, schedule);
    }

    [Theory]
    [InlineData(typeof(MaterializeRecurringBookingsFunction), MaterializeCron)]
    [InlineData(typeof(SendRecurringOrderRemindersFunction), RemindersCron)]
    [InlineData(typeof(SendMembershipLifecycleNotificationsFunction), MembershipCron)]
    [InlineData(typeof(SendNewJobsDigestTimerFunction), DigestCron)]
    [InlineData(typeof(ExpireStaleReferralsFunction), ExpireReferralsCron)]
    [InlineData(typeof(SendPreCleaningRemindersFunction), PreCleaningCron)]
    [InlineData(typeof(SendCleanerJobRemindersFunction), CleanerJobRemindersCron)]
    [InlineData(typeof(SendTomorrowJobDigestFunction), TomorrowDigestCron)]
    public void Effective_schedule_equals_documented_production_cadence(Type functionType, string expectedCron)
    {
        var token = ReadSchedule(functionType);

        var effective = ResolveToken(token);

        Assert.Equal(expectedCron, effective);
    }

    /// <summary>
    /// The cadence as a PROPERTY of the schedule rather than a string comparison: the digest opens at
    /// most one notification window per clock hour. String equality above pins the committed default;
    /// this pins what the owner actually asked for, so any expression that reintroduces a sub-hourly
    /// window — <c>0 0,30</c>, <c>0 *&#47;20</c>, a second minute field entry — fails here whatever it
    /// is spelled like. The digest watermark advances to the sweep start, so the sweep interval IS the
    /// per-cleaner rate limit.
    /// </summary>
    [Fact]
    public void Digest_opens_at_most_one_window_per_clock_hour()
    {
        var digest = CronSchedule.Parse(ResolveToken(ReadSchedule(typeof(SendNewJobsDigestTimerFunction))));

        var dayStart = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var fires = new List<DateTime>();
        var cursor = dayStart.AddSeconds(-1);
        while (true)
        {
            cursor = digest.NextOccurrence(cursor);
            if (cursor >= dayEnd)
            {
                break;
            }

            fires.Add(cursor);
        }

        Assert.Equal(
            fires.Select(f => f.Hour).Distinct().Count(),
            fires.Count);
        Assert.Equal(24, fires.Select(f => f.Hour).Distinct().Count());
    }

    /// <summary>
    /// The one relationship that makes the pre-cleaning promise keepable, as a property rather than a
    /// pair of strings that happen to agree today. The sweep only reminds orders whose cleaning falls
    /// inside a window <c>LeadMinutesHigh - LeadMinutesLow</c> wide; if two consecutive fires are
    /// further apart than that, an order can pass through the window between them and be reminded
    /// never. Widening the cron or narrowing the window fails here, whichever moves.
    /// </summary>
    [Fact]
    public void Pre_cleaning_sweep_fires_at_least_once_per_reminder_window()
    {
        var window = new SendPreCleaningReminders.Command();
        var windowMinutes = window.LeadMinutesHigh - window.LeadMinutesLow;
        var schedule = CronSchedule.Parse(ResolveToken(ReadSchedule(typeof(SendPreCleaningRemindersFunction))));

        var dayStart = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var previous = dayStart;
        var widestGap = TimeSpan.Zero;
        for (var fire = schedule.NextOccurrence(dayStart); fire < dayEnd; fire = schedule.NextOccurrence(fire))
        {
            widestGap = fire - previous > widestGap ? fire - previous : widestGap;
            previous = fire;
        }

        Assert.True(
            widestGap.TotalMinutes <= windowMinutes,
            $"the sweep's widest gap is {widestGap.TotalMinutes} minutes but its window is only " +
            $"{windowMinutes} minutes wide — an order can cross the window between two fires and never " +
            "be reminded.");
    }

    /// <summary>
    /// <b>The one relation the grace window rests on, and the only one that was not pinned.</b>
    ///
    /// <para>ADR-0055 argues 60 minutes is safe because a cleaner who may start an hour early still gets
    /// their customer's "starting soon" notice first. That is only true while the sweep's FIRST fire
    /// inside its window lands later than the grace allows — and that margin comes from the cron's
    /// phase, not from the constant. Widen the cron to 15 minutes, or narrow the window, and the
    /// guarantee silently evaporates with nothing else failing.</para>
    ///
    /// <para>Both operands are already computed by the cadence test above; this is the assertion nobody
    /// wrote.</para>
    /// </summary>
    [Fact]
    public void The_start_grace_window_cannot_outrun_the_customers_pre_cleaning_notice()
    {
        var window = new SendPreCleaningReminders.Command();
        var schedule = CronSchedule.Parse(ResolveToken(ReadSchedule(typeof(SendPreCleaningRemindersFunction))));

        var dayStart = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);
        var previous = dayStart;
        var widestGap = TimeSpan.Zero;
        for (var fire = schedule.NextOccurrence(dayStart); fire < dayEnd; fire = schedule.NextOccurrence(fire))
        {
            widestGap = fire - previous > widestGap ? fire - previous : widestGap;
            previous = fire;
        }

        // Worst case the notice goes out at LeadMinutesHigh minus one whole gap. The grace must stay
        // strictly inside that, or a cleaner can be at the door before the customer has been told.
        var guaranteedNoticeMinutes = window.LeadMinutesHigh - widestGap.TotalMinutes;

        Assert.True(
            BookingPolicy.StartGraceWindowMinutes < guaranteedNoticeMinutes,
            $"the start grace is {BookingPolicy.StartGraceWindowMinutes} minutes but the customer's " +
            $"notice is only guaranteed by T-{guaranteedNoticeMinutes} (window high " +
            $"{window.LeadMinutesHigh} minus a widest cron gap of {widestGap.TotalMinutes}) — a cleaner " +
            "can start, and the customer finds out afterwards.");
    }

    /// <summary>
    /// The window straddles the hour the customer was promised, and is tight enough that "about an
    /// hour" is true at both ends. A window that had drifted off 60 would still satisfy the cadence
    /// property above while making the copy false.
    /// </summary>
    [Fact]
    public void The_pre_cleaning_window_brackets_the_promised_hour()
    {
        const int PromisedLeadMinutes = 60;
        var window = new SendPreCleaningReminders.Command();

        Assert.InRange(PromisedLeadMinutes, window.LeadMinutesLow, window.LeadMinutesHigh);
        Assert.InRange(window.LeadMinutesHigh - window.LeadMinutesLow, 1, 20);
    }

    [Fact]
    public void Materialize_fires_strictly_before_Reminder()
    {
        var materialize = CronSchedule.Parse(ResolveToken(ReadSchedule(typeof(MaterializeRecurringBookingsFunction))));
        var reminder = CronSchedule.Parse(ResolveToken(ReadSchedule(typeof(SendRecurringOrderRemindersFunction))));

        var dayStart = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var materializeFire = materialize.NextOccurrence(dayStart);
        var reminderFire = reminder.NextOccurrence(dayStart);

        Assert.True(
            materializeFire < reminderFire,
            $"Materialize ({materializeFire:O}) must fire strictly before Reminder ({reminderFire:O}).");
    }

    private static string ReadSchedule(Type functionType)
    {
        var run = functionType.GetMethod("Run", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{functionType.Name} has no public Run method.");

        var timerParam = run.GetParameters()
            .Single(p => p.ParameterType == typeof(TimerInfo));

        var attr = timerParam.GetCustomAttribute<TimerTriggerAttribute>()
            ?? throw new InvalidOperationException($"{functionType.Name}.Run has no [TimerTrigger].");

        return attr.Schedule;
    }

    private static string ResolveToken(string token)
    {
        var key = token.Trim('%');
        var value = ProductionDefaults[key];

        Assert.False(
            string.IsNullOrWhiteSpace(value),
            $"Production default for app-setting '{key}' is missing from the committed Functions config.");

        return value!;
    }

    private static IConfiguration BuildProductionDefaults()
    {
        // Walk up to the solution directory instead of a fixed ..\..\..\.. hop count, so the
        // lookup survives a non-default test output path (bin depth is a build detail).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && dir.GetFiles("*.sln").Length == 0)
        {
            dir = dir.Parent;
        }

        Assert.False(dir is null, "Could not locate the solution directory from the test base directory.");

        var appSettings = Path.Combine(dir!.FullName, "Cleansia.Functions", "appsettings.json");

        Assert.True(
            File.Exists(appSettings),
            $"Committed Functions production defaults not found at '{appSettings}'.");

        return new ConfigurationBuilder()
            .AddJsonFile(appSettings, optional: false)
            .Build();
    }

    // Minimal 6-field (sec min hour dom mon dow) cron next-occurrence calculator, scoped to the fixed
    // daily / half-hourly cadences these four timers use. Keeps the schedule assertion dependency-free.
    private sealed class CronSchedule
    {
        private readonly int[] _seconds;
        private readonly int[] _minutes;
        private readonly int[] _hours;

        private CronSchedule(int[] seconds, int[] minutes, int[] hours)
        {
            _seconds = seconds;
            _minutes = minutes;
            _hours = hours;
        }

        public static CronSchedule Parse(string expression)
        {
            var fields = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length != 6)
            {
                throw new FormatException($"Expected a 6-field cron expression, got '{expression}'.");
            }

            return new CronSchedule(
                ParseField(fields[0], 0, 59),
                ParseField(fields[1], 0, 59),
                ParseField(fields[2], 0, 23));
        }

        public DateTime NextOccurrence(DateTime after)
        {
            var cursor = new DateTime(after.Year, after.Month, after.Day, 0, 0, 0, DateTimeKind.Utc);

            for (var dayOffset = 0; dayOffset <= 1; dayOffset++)
            {
                var day = cursor.AddDays(dayOffset);

                foreach (var hour in _hours)
                {
                    foreach (var minute in _minutes)
                    {
                        foreach (var second in _seconds)
                        {
                            var candidate = day.AddHours(hour).AddMinutes(minute).AddSeconds(second);

                            if (candidate > after)
                            {
                                return candidate;
                            }
                        }
                    }
                }
            }

            throw new InvalidOperationException($"No occurrence found after {after:O}.");
        }

        private static int[] ParseField(string field, int min, int max)
        {
            if (field == "*")
            {
                return Enumerable.Range(min, max - min + 1).ToArray();
            }

            if (field.StartsWith("*/", StringComparison.Ordinal))
            {
                var step = int.Parse(field[2..]);
                return Enumerable.Range(min, max - min + 1).Where(v => v % step == 0).ToArray();
            }

            return field
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .OrderBy(v => v)
                .ToArray();
        }
    }
}

using System.Reflection;
using Cleansia.Functions.Functions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Tests.Functions;

// F5 / AC5-AC6 — the four recurring/notification timers must read their cron from an app-setting via
// %AppSetting% TimerTrigger syntax (promotion is config-only), the committed production defaults must
// match each function's documented cadence, and Materialize must fire strictly before Reminder.
public class TimerScheduleConfigTests
{
    private const string MaterializeToken = "%MaterializeRecurringBookingsCron%";
    private const string RemindersToken = "%SendRecurringOrderRemindersCron%";
    private const string MembershipToken = "%SendMembershipLifecycleNotificationsCron%";
    private const string DigestToken = "%SendNewJobsDigestCron%";
    private const string ExpireReferralsToken = "%ExpireStaleReferralsCron%";

    private const string MaterializeCron = "0 0 2 * * *";
    private const string RemindersCron = "0 30 2 * * *";
    private const string MembershipCron = "0 0 3 * * *";
    private const string DigestCron = "0 0,30 * * * *";
    private const string ExpireReferralsCron = "0 30 3 * * *";

    private static readonly IConfiguration ProductionDefaults = BuildProductionDefaults();

    [Theory]
    [InlineData(typeof(MaterializeRecurringBookingsFunction), MaterializeToken)]
    [InlineData(typeof(SendRecurringOrderRemindersFunction), RemindersToken)]
    [InlineData(typeof(SendMembershipLifecycleNotificationsFunction), MembershipToken)]
    [InlineData(typeof(SendNewJobsDigestTimerFunction), DigestToken)]
    [InlineData(typeof(ExpireStaleReferralsFunction), ExpireReferralsToken)]
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
    public void Effective_schedule_equals_documented_production_cadence(Type functionType, string expectedCron)
    {
        var token = ReadSchedule(functionType);

        var effective = ResolveToken(token);

        Assert.Equal(expectedCron, effective);
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
        var functionsDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Cleansia.Functions"));

        var appSettings = Path.Combine(functionsDir, "appsettings.json");

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

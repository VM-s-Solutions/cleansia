using System.Text.Json;

namespace Cleansia.Tests.Functions;

/// <summary>
/// The Functions host.json settings whose only job is to keep the Application Insights bill down.
/// They are invisible at runtime — nothing fails if one is reverted, the invoice just grows again
/// (~€35–42/month: 14 queue listeners polling every 5s is ~7.3M billed dependency rows) — so they get
/// a test instead of a comment. host.json admits no comments, which is the other reason this file
/// exists: it is where the reasoning for each value lives.
/// </summary>
public class HostJsonTelemetryCostTests
{
    private static JsonElement HostJson()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "host.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }

    [Fact]
    public void QueuePollingIntervalIsThirtySeconds()
    {
        // The empty-queue backoff ceiling. Each poll is a billed dependency row across 14 listeners
        // (QueueListenerInventoryTests derives that count), so 5s (the previous value) is ~6x the
        // telemetry of 30s for no throughput gain — a message still wakes the listener immediately once
        // the queue is non-empty; the ceiling only bounds how long an IDLE listener sleeps before the
        // next look.
        //
        // The latency this buys, stated per consumer rather than assumed uniform. Seven of the fourteen
        // listeners are poison companions: they exist to dead-letter and alert, and the alert fires off
        // the storage PutMessage log rather than off the consumer, so their pickup latency is not on any
        // alerting path. Of the seven live ones, the two a user can perceive are notifications-dispatch
        // and live-activity-dispatch — and BOTH are fed through the outbox, so the ceiling here is only
        // the second of three legs: drainer tick <=10s, this backoff <=30s, then the handler. Worst case
        // ~40s to a lock screen; typical much less, since the ceiling is only reached by a queue that has
        // been idle. That is the budget. Nothing here is sub-minute-critical, and nothing is time-of-day
        // critical (the schedule-driven work is on timer triggers, not queues).
        //
        // It is ONE value for every queue and every environment, and neither is expressible today:
        // extensions.queues is host-global with no per-queue override, and per-environment expression
        // would need an AzureFunctionsJobHost__extensions__queues__maxPollingInterval app setting, which
        // nothing sets. Dev is the only environment ever deployed, so the single value is dev's; a prod
        // that wants faster pickup buys it with that app setting, not by editing this file.
        var polling = HostJson()
            .GetProperty("extensions").GetProperty("queues")
            .GetProperty("maxPollingInterval").GetString();

        Assert.Equal("00:00:30", polling);
    }

    [Fact]
    public void SamplingExcludesOnlyExceptions()
    {
        // Anything listed here is exempt from adaptive sampling, i.e. billed 1:1 forever. Exceptions
        // must be (we need every one); Requests must NOT be — a routine invocation is the highest-volume
        // item the host emits and excluding it meant sampling never actually engaged.
        var excluded = HostJson()
            .GetProperty("logging").GetProperty("applicationInsights")
            .GetProperty("samplingSettings").GetProperty("excludedTypes").GetString();

        Assert.Equal("Exception", excluded);
    }

    [Fact]
    public void SamplingHasAnItemsPerSecondCeiling()
    {
        var cap = HostJson()
            .GetProperty("logging").GetProperty("applicationInsights")
            .GetProperty("samplingSettings").GetProperty("maxTelemetryItemsPerSecond").GetInt32();

        Assert.InRange(cap, 1, 5);
    }

    [Fact]
    public void DefaultLogLevelIsRaisedButFunctionLogsAndOutcomesSurvive()
    {
        var logLevel = HostJson().GetProperty("logging").GetProperty("logLevel");

        Assert.Equal("Warning", logLevel.GetProperty("default").GetString());
        // The two categories that must stay verbose: our own function bodies, and the host's
        // per-invocation pass/fail line.
        Assert.Equal("Information", logLevel.GetProperty("Function").GetString());
        Assert.Equal("Information", logLevel.GetProperty("Host.Results").GetString());
        // Host.Aggregator at Trace emitted a metrics stream nobody reads. Absent = inherits Warning.
        Assert.False(logLevel.TryGetProperty("Host.Aggregator", out _));
    }
}

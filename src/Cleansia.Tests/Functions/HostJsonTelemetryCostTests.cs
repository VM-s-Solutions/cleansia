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
        // The empty-queue backoff ceiling. Each poll is a billed dependency row across 14 listeners, so
        // 5s (the previous value) is ~6x the telemetry of 30s for no throughput gain — a message still
        // wakes the listener immediately once the queue is non-empty; the ceiling only bounds how long
        // an IDLE listener sleeps before the next look.
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

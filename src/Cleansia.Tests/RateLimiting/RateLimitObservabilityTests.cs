using System.Diagnostics.Metrics;
using Cleansia.Config.RateLimiting;
using Cleansia.Tests.RateLimiting.Harness;

namespace Cleansia.Tests.RateLimiting;

/// <summary>
/// ADR-0003 D8 / AC12 (verify #10) — the control's own observability, S6-safe. A rejection emits a
/// counter dimensioned by POLICY NAME ONLY (never the partition key / IP / sub); a partition-count
/// gauge is exported; a degraded-mode signal fires when forwarded-headers are unconfigured.
/// </summary>
[Collection("RateLimiterHost")]
public class RateLimitObservabilityTests
{
    private sealed record Measurement(string Instrument, long Value, IReadOnlyDictionary<string, object?> Tags);

    private static (MeterListener listener, List<Measurement> measurements) ListenToRateLimitMeter()
    {
        var measurements = new List<Measurement>();
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == RateLimitMetrics.MeterName)
                    l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((inst, val, tags, _) =>
        {
            var dict = new Dictionary<string, object?>();
            foreach (var t in tags) dict[t.Key] = t.Value;
            measurements.Add(new Measurement(inst.Name, val, dict));
        });
        listener.Start();
        return (listener, measurements);
    }

    // AC12 — rejection counter is dimensioned by policy name ONLY; the key (IP/sub) never appears.
    [Fact]
    public async Task Rejection_Emits_Counter_By_Policy_Name_Only()
    {
        var (listener, measurements) = ListenToRateLimitMeter();
        try
        {
            await using var h = await RateLimiterHostHarness.StartAsync(RateLimitTestConfig.Valid(authAnon: 1));
            await h.SendAsync("/auth", xffClientIp: "10.9.9.9"); // permit 1 consumed
            await h.SendAsync("/auth", xffClientIp: "10.9.9.9"); // rejected → counter fires
        }
        finally
        {
            listener.Dispose();
        }

        var rejections = measurements.Where(m => m.Instrument == RateLimitMetrics.RejectionCounterName).ToList();
        Assert.NotEmpty(rejections);

        var withPolicy = rejections.Where(m =>
            m.Tags.TryGetValue("policy", out var p) && (p as string) == "auth").ToList();
        Assert.NotEmpty(withPolicy);

        // S6: the partition key (IP / sub) must NEVER be a tag value or key.
        foreach (var m in rejections)
        {
            foreach (var kv in m.Tags)
            {
                Assert.DoesNotContain("10.9.9.9", kv.Value?.ToString() ?? "");
                Assert.DoesNotContain("ip:", kv.Key);
                Assert.DoesNotContain("sub:", kv.Key);
            }
        }
    }

    // AC12 — a degraded-mode signal is emitted when forwarded-headers are unconfigured (dev).
    [Fact]
    public void Degraded_Signal_Fires_When_ForwardedHeaders_Unconfigured()
    {
        var (listener, measurements) = ListenToRateLimitMeter();
        try
        {
            RateLimitMetrics.SignalDegradedForwardedHeaders("test-unconfigured");
            listener.RecordObservableInstruments();
        }
        finally
        {
            listener.Dispose();
        }

        Assert.Contains(measurements, m => m.Instrument == RateLimitMetrics.DegradedCounterName);
    }

    // AC12 — a partition-count gauge instrument exists and is observable.
    [Fact]
    public void PartitionCount_Gauge_Is_Exported()
    {
        var (listener, measurements) = ListenToRateLimitMeter();
        try
        {
            RateLimitMetrics.SetPartitionCount("auth", 3);
            listener.RecordObservableInstruments();
        }
        finally
        {
            listener.Dispose();
        }

        Assert.Contains(measurements, m => m.Instrument == RateLimitMetrics.PartitionGaugeName);
    }
}

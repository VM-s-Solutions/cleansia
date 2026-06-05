using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Config.RateLimiting;

/// <summary>
/// ADR-0003 D8 — observability of the rate-limiter's own health, S6-safe. Everything here is
/// dimensioned by POLICY NAME ONLY — never by the partition key (which embeds the client IP or the
/// JWT <c>sub</c> and is PII-adjacent). Feeds the existing App Insights metric pipeline via the
/// OpenTelemetry meter bridge (the meter name is what App Insights subscribes to).
/// </summary>
public static class RateLimitMetrics
{
    public const string MeterName = "Cleansia.RateLimiting";
    public const string RejectionCounterName = "cleansia.ratelimit.rejections";
    public const string DegradedCounterName = "cleansia.ratelimit.forwarded_headers_degraded";
    public const string PartitionGaugeName = "cleansia.ratelimit.partitions";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Rejections =
        Meter.CreateCounter<long>(RejectionCounterName, unit: "{rejection}",
            description: "429 rate-limit rejections, dimensioned by policy name only (S6).");

    private static readonly Counter<long> Degraded =
        Meter.CreateCounter<long>(DegradedCounterName, unit: "{event}",
            description: "Forwarded-headers ran in degraded/single-bucket mode (dev or misconfig).");

    // Latest observed live-partition count per policy (D7 early warning). Read by an observable gauge.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> PartitionCounts = new();

    static RateLimitMetrics()
    {
        Meter.CreateObservableGauge(PartitionGaugeName,
            () => PartitionCounts.Select(kv => new Measurement<long>(kv.Value, new KeyValuePair<string, object?>("policy", kv.Key))),
            unit: "{partition}",
            description: "Live rate-limit partitions per policy (cardinality early warning, D7).");
    }

    /// <summary>Records a 429 rejection. The policy name is read from the rejected lease's
    /// endpoint/policy context; the partition key is deliberately NOT recorded (S6).</summary>
    public static void RecordRejection(OnRejectedContext context)
    {
        var policy = ResolvePolicyName(context);
        Rejections.Add(1, new KeyValuePair<string, object?>("policy", policy));
    }

    /// <summary>Emits the degraded-mode signal (D8 #3) — forwarded-headers unconfigured (dev) or a
    /// runtime regression. <paramref name="reason"/> is a low-cardinality, PII-free label.</summary>
    public static void SignalDegradedForwardedHeaders(string reason) =>
        Degraded.Add(1, new KeyValuePair<string, object?>("reason", reason));

    /// <summary>Updates the partition-count gauge value for a policy (D7 observability).</summary>
    public static void SetPartitionCount(string policy, long count) => PartitionCounts[policy] = count;

    private static string ResolvePolicyName(OnRejectedContext context)
    {
        // The rejected request's path/policy is not directly on the lease; we tag by the policy the
        // endpoint opted into, recovered from the endpoint metadata. Fall back to "unknown" rather
        // than ever leaking the key.
        var endpoint = context.HttpContext.GetEndpoint();
        var meta = endpoint?.Metadata.GetMetadata<EnableRateLimitingAttribute>();
        return meta?.PolicyName ?? "unknown";
    }
}

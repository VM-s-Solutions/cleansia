using System.Diagnostics.Metrics;
using Cleansia.Config.RateLimiting;
using Cleansia.Tests.RateLimiting.Harness;

namespace Cleansia.Tests.RateLimiting;

/// <summary>
/// T-0116 (SEC-W3) — the Stripe-webhook per-source-IP rate-limit window, proven on the real
/// rate-limiter middleware via <see cref="RateLimiterHostHarness"/> (TestServer, no Docker / Stripe /
/// Postgres). The harness maps the genuine <c>"webhook"</c> named policy onto
/// <c>POST /api/Payment/webhook</c> in the ADR-mandated pipeline order, so these cases exercise the
/// SAME registration <c>CleansiaStartupBase</c> uses in production.
///
/// TEST-FIRST: every case here fails against the pre-fix tree (no <c>"webhook"</c> policy registered
/// in <see cref="RateLimitPolicies"/> ⇒ the route's <c>RequireRateLimiting("webhook")</c> resolves no
/// policy and the limiter never throttles) and passes once the third policy lands.
/// </summary>
[Collection("RateLimiterHost")] // mutate process-wide limiter partitions + the shared static Meter; keep serial
public class WebhookRateLimitTests
{
    private const string WebhookPath = "/api/Payment/webhook";

    // AC2 / AC7 — per-source-IP isolation on the webhook route: IP A exhausts the window → A's next
    // POST is 429; IP B's first POST in the same window is NOT 429 (distinct IP = distinct partition).
    [Fact]
    public async Task AC2_Webhook_PerSourceIp_Isolation()
    {
        await using var h = await RateLimiterHostHarness.StartAsync(RateLimitTestConfig.Valid(webhook: 5));

        // Source IP A (203.0.113.10) spends all 5 webhook permits.
        for (var i = 1; i <= 5; i++)
        {
            var r = await h.SendAsync(WebhookPath, xffClientIp: "203.0.113.10");
            Assert.False(r.Is429, $"A webhook #{i} should be allowed (got {r.StatusCode}); resolved={r.ResolvedClientIp}");
        }

        // A's next POST in the same window → 429 (per-IP window exhausted).
        var a6 = await h.SendAsync(WebhookPath, xffClientIp: "203.0.113.10");
        Assert.True(a6.Is429, "A's 6th webhook POST must be 429 (per-source-IP window exhausted).");

        // Source IP B (203.0.113.20) first POST in the same window → NOT 429 (isolated partition).
        var b1 = await h.SendAsync(WebhookPath, xffClientIp: "203.0.113.20");
        Assert.False(b1.Is429, "B's first webhook POST must NOT be 429 — distinct source IP = distinct partition.");
    }

    // AC3 / AC7 (verify #11) — independence: flooding "webhook" to 429 consumes NO "auth"/"interactive"
    // allowance. Same source IP, but each named policy has its own partition tree.
    [Fact]
    public async Task AC3_Webhook_Flood_Does_Not_Consume_Auth_Or_Interactive_Allowance()
    {
        await using var h = await RateLimiterHostHarness.StartAsync(
            RateLimitTestConfig.Valid(authAnon: 10, interactive: 5, webhook: 3));

        const string ip = "198.51.100.5";

        // Flood the webhook policy from this IP to rejection (4th over webhook=3 → 429).
        for (var i = 1; i <= 3; i++)
            Assert.False((await h.SendAsync(WebhookPath, xffClientIp: ip)).Is429, $"webhook #{i} should pass under webhook=3");
        Assert.True((await h.SendAsync(WebhookPath, xffClientIp: ip)).Is429, "4th webhook over =3 must be 429");

        // The SAME IP's auth + interactive allowances are untouched — they are separate named partitions.
        var auth1 = await h.SendAsync("/auth", xffClientIp: ip);
        Assert.False(auth1.Is429, "auth allowance must be unaffected by a webhook flood (independent partition).");
        var interactive1 = await h.SendAsync("/interactive", xffClientIp: ip);
        Assert.False(interactive1.Is429, "interactive allowance must be unaffected by a webhook flood (independent partition).");
    }

    // AC3 / AC7 (verify #11), reverse direction — exhausting "auth" consumes NO "webhook" allowance.
    [Fact]
    public async Task AC3_Auth_Flood_Does_Not_Consume_Webhook_Allowance()
    {
        await using var h = await RateLimiterHostHarness.StartAsync(
            RateLimitTestConfig.Valid(authAnon: 2, webhook: 5));

        const string ip = "198.51.100.6";

        // Exhaust the anonymous auth window from this IP (3rd over authAnon=2 → 429).
        for (var i = 1; i <= 2; i++)
            Assert.False((await h.SendAsync("/auth", xffClientIp: ip)).Is429, $"auth #{i} should pass under authAnon=2");
        Assert.True((await h.SendAsync("/auth", xffClientIp: ip)).Is429, "3rd auth over =2 must be 429");

        // The webhook window for the same IP is full and untouched.
        var webhook1 = await h.SendAsync(WebhookPath, xffClientIp: ip);
        Assert.False(webhook1.Is429, "webhook allowance must be unaffected by an auth flood (independent partition).");
    }

    // AC5 / AC7 — a rejected webhook returns 429 + a positive integer Retry-After (inherits the shared
    // OnRejected / RejectionStatusCode from T-0115; QueueLimit=0 → immediate reject, never queued).
    [Fact]
    public async Task AC5_Rejected_Webhook_Returns_429_With_RetryAfter()
    {
        await using var h = await RateLimiterHostHarness.StartAsync(RateLimitTestConfig.Valid(webhook: 1));

        Assert.False((await h.SendAsync(WebhookPath, xffClientIp: "192.0.2.7")).Is429, "first webhook (permit 1) allowed");
        var rejected = await h.SendAsync(WebhookPath, xffClientIp: "192.0.2.7");
        Assert.True(rejected.Is429, "2nd webhook over =1 must be 429.");
        Assert.False(string.IsNullOrEmpty(rejected.RetryAfter), "429 webhook must carry a Retry-After header (D6/AC5).");
        Assert.True(int.TryParse(rejected.RetryAfter, out var secs) && secs > 0, "Retry-After must be a positive integer.");
    }

    // AC5 — the D8 rejection metric is dimensioned by the policy name "webhook" ONLY; the partition key
    // (the source IP) never appears as a tag key or value (S6).
    [Fact]
    public async Task AC5_Rejection_Metric_Is_Policy_Name_Only_Never_The_Ip()
    {
        var measurements = new List<(string Instrument, IReadOnlyDictionary<string, object?> Tags)>();
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == RateLimitMetrics.MeterName)
                    l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((inst, _, tags, _) =>
        {
            var dict = new Dictionary<string, object?>();
            foreach (var t in tags) dict[t.Key] = t.Value;
            measurements.Add((inst.Name, dict));
        });
        listener.Start();

        const string ip = "192.0.2.8";
        try
        {
            await using var h = await RateLimiterHostHarness.StartAsync(RateLimitTestConfig.Valid(webhook: 1));
            await h.SendAsync(WebhookPath, xffClientIp: ip); // permit 1 consumed
            await h.SendAsync(WebhookPath, xffClientIp: ip); // rejected → counter fires
        }
        finally
        {
            listener.Dispose();
        }

        var rejections = measurements.Where(m => m.Instrument == RateLimitMetrics.RejectionCounterName).ToList();
        Assert.NotEmpty(rejections);
        Assert.Contains(rejections, m => m.Tags.TryGetValue("policy", out var p) && (p as string) == "webhook");

        // S6: the source IP must NEVER be a tag value, and the partition prefix never a tag key.
        foreach (var m in rejections)
            foreach (var kv in m.Tags)
            {
                Assert.DoesNotContain(ip, kv.Value?.ToString() ?? "");
                Assert.DoesNotContain("ip:", kv.Key);
                Assert.DoesNotContain("webhook:", kv.Key);
            }
    }
}

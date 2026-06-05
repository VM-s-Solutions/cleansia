using Cleansia.Tests.RateLimiting.Harness;
using Microsoft.Extensions.Hosting;

namespace Cleansia.Tests.RateLimiting;

/// <summary>
/// ADR-0003 "How a reviewer verifies" #2,#3,#4,#7,#8,#9 — the BEHAVIORAL gate, run through the real
/// rate-limiter middleware via <see cref="RateLimiterHostHarness"/> (TestServer, no Docker). Each
/// test boots the real <c>RateLimitPolicies</c> registration + <c>ForwardedHeadersOptions</c> in the
/// ADR-mandated pipeline order and drives synthetic-XFF requests.
/// </summary>
[Collection("RateLimiterHost")] // these mutate process-wide limiter partitions; keep serial
public class RateLimiterHostBehaviorTests
{
    // AC1 / verify #2 — per-IP isolation: A's 11th 429, B's first not 429.
    [Fact]
    public async Task AC1_PerIp_Isolation_Anonymous()
    {
        await using var h = await RateLimiterHostHarness.StartAsync(RateLimitTestConfig.Valid(authAnon: 10));

        // Client A (10.0.0.1) spends all 10 anonymous auth permits.
        for (var i = 1; i <= 10; i++)
        {
            var r = await h.SendAsync("/auth", xffClientIp: "10.0.0.1");
            Assert.False(r.Is429, $"A request #{i} should be allowed (got {r.StatusCode}); resolved={r.ResolvedClientIp}");
        }

        // A's 11th in the same window → 429.
        var a11 = await h.SendAsync("/auth", xffClientIp: "10.0.0.1");
        Assert.True(a11.Is429, "A's 11th anonymous auth request must be 429 (per-IP window exhausted).");

        // Client B (10.0.0.2) first request → NOT 429 (isolated partition).
        var b1 = await h.SendAsync("/auth", xffClientIp: "10.0.0.2");
        Assert.False(b1.Is429, "B's first request must NOT be 429 — distinct IP = distinct partition.");
    }

    // AC2 / verify #3 — per-sub isolation: X exhausts, Y unaffected from the SAME IP.
    [Fact]
    public async Task AC2_PerSub_Isolation_Authenticated()
    {
        await using var h = await RateLimiterHostHarness.StartAsync(RateLimitTestConfig.Valid(authAuthenticated: 30));

        // User X exhausts X's 30 authenticated-auth permits (same IP as Y).
        for (var i = 1; i <= 30; i++)
        {
            var r = await h.SendAsync("/auth", xffClientIp: "10.0.0.50", sub: "user-X");
            Assert.False(r.Is429, $"X request #{i} should be allowed (got {r.StatusCode}).");
        }
        var x31 = await h.SendAsync("/auth", xffClientIp: "10.0.0.50", sub: "user-X");
        Assert.True(x31.Is429, "X's 31st must be 429 (sub window exhausted).");

        // User Y, SAME IP, different sub → unaffected.
        var y1 = await h.SendAsync("/auth", xffClientIp: "10.0.0.50", sub: "user-Y");
        Assert.False(y1.Is429, "Y (different sub, same IP) must NOT be 429.");
    }

    // AC4 / verify #4 — trusted XFF honored; untrusted hop ignored (keyed by connection IP).
    [Fact]
    public async Task AC4_Trusted_Xff_Honored_Distinct_Partitions()
    {
        await using var h = await RateLimiterHostHarness.StartAsync(RateLimitTestConfig.Valid(authAnon: 10));

        var r1 = await h.SendAsync("/auth", xffClientIp: "203.0.113.10"); // peer = trusted 127.0.0.1
        var r2 = await h.SendAsync("/auth", xffClientIp: "203.0.113.20");
        Assert.Equal("203.0.113.10", r1.ResolvedClientIp);
        Assert.Equal("203.0.113.20", r2.ResolvedClientIp);
    }

    [Fact]
    public async Task AC4_Spoofed_Xff_From_Untrusted_Peer_Is_Ignored()
    {
        await using var h = await RateLimiterHostHarness.StartAsync(RateLimitTestConfig.Valid(authAnon: 10));

        // Connection peer 8.8.8.8 is NOT in KnownProxies → XFF must be ignored; keyed by 8.8.8.8.
        var r = await h.SendAsync("/auth", xffClientIp: "203.0.113.99", connectionPeer: "8.8.8.8");
        Assert.Equal("8.8.8.8", r.ResolvedClientIp);
    }

    // AC8 / verify #7 — sub limit (30) is looser than the IP limit (10): an authed caller gets >10.
    [Fact]
    public async Task AC8_Authenticated_Auth_Limit_Is_30_Not_10()
    {
        await using var h = await RateLimiterHostHarness.StartAsync(
            RateLimitTestConfig.Valid(authAnon: 10, authAuthenticated: 30));

        // 11 authenticated requests must all pass (would 429 at 11 under the IP limit).
        for (var i = 1; i <= 11; i++)
        {
            var r = await h.SendAsync("/auth", xffClientIp: "10.0.0.60", sub: "user-Q");
            Assert.False(r.Is429, $"authed request #{i} should pass under the 30/min sub limit.");
        }
    }

    // AC8 — interactive limit is 60 and per-caller (use a small override to keep the test quick).
    [Fact]
    public async Task AC8_Interactive_Limit_Is_PerCaller()
    {
        await using var h = await RateLimiterHostHarness.StartAsync(RateLimitTestConfig.Valid(interactive: 3));

        for (var i = 1; i <= 3; i++)
            Assert.False((await h.SendAsync("/interactive", xffClientIp: "10.1.0.1")).Is429);
        Assert.True((await h.SendAsync("/interactive", xffClientIp: "10.1.0.1")).Is429, "4th over interactive=3 → 429");
        // distinct caller unaffected
        Assert.False((await h.SendAsync("/interactive", xffClientIp: "10.1.0.2")).Is429);
    }

    // AC9 / verify #8 — global anonymous cardinality cap: past the ceiling, new anon IPs get 429.
    [Fact]
    public async Task AC9_Anonymous_Global_Cardinality_Cap_Rejects_Past_Ceiling()
    {
        // Tight ceiling (5) so distinct-IP spray is bounded; per-IP limit high so the IP window
        // is not the thing rejecting — only the GLOBAL cap is.
        await using var h = await RateLimiterHostHarness.StartAsync(
            RateLimitTestConfig.Valid(authAnon: 1000, anonGlobalCeiling: 5));

        var allowed = 0;
        var rejected = 0;
        for (var i = 0; i < 20; i++)
        {
            var r = await h.SendAsync("/auth", xffClientIp: $"198.51.100.{i}"); // 20 distinct IPs
            if (r.Is429) rejected++; else allowed++;
        }
        Assert.True(allowed <= 5, $"global ceiling should cap distinct anonymous admissions at ~5 (allowed={allowed}).");
        Assert.True(rejected > 0, "spraying 20 distinct IPs past a ceiling of 5 must produce 429s.");
    }

    // AC10 — every 429 carries a Retry-After header.
    [Fact]
    public async Task AC10_Rejection_Carries_RetryAfter()
    {
        await using var h = await RateLimiterHostHarness.StartAsync(RateLimitTestConfig.Valid(authAnon: 1));

        Assert.False((await h.SendAsync("/auth", xffClientIp: "10.2.0.1")).Is429);
        var rejected = await h.SendAsync("/auth", xffClientIp: "10.2.0.1");
        Assert.True(rejected.Is429);
        Assert.False(string.IsNullOrEmpty(rejected.RetryAfter), "429 must carry a Retry-After header (D6/AC10).");
        Assert.True(int.TryParse(rejected.RetryAfter, out var secs) && secs > 0, "Retry-After must be a positive integer.");
    }

    // AC11 / verify #9 — honest authenticated checkout (8 mutations < 60s) under the 30/min sub limit: no 429.
    [Fact]
    public async Task AC11_Honest_Checkout_Session_No_False_429()
    {
        await using var h = await RateLimiterHostHarness.StartAsync(RateLimitTestConfig.Valid(authAuthenticated: 30));

        // create-order + 3 declined-card retries + re-quote + confirm ≈ 8 mutations, one customer.
        for (var i = 1; i <= 8; i++)
        {
            var r = await h.SendAsync("/auth", xffClientIp: "203.0.113.200", sub: "honest-customer");
            Assert.False(r.Is429, $"honest checkout mutation #{i} must NOT be throttled under the 30/min sub limit.");
        }
    }

    // AC4/AC5 negative control — in Development an empty forwarded-headers config still BOOTS.
    [Fact]
    public async Task Dev_Empty_ForwardedHeaders_Boots_Degraded()
    {
        var cfg = RateLimitTestConfig.Valid();
        cfg["ForwardedHeaders:KnownProxies"] = "";
        cfg["ForwardedHeaders:KnownNetworks"] = "";
        var ex = await RateLimiterHostHarness.TryBootThrows(cfg, Environments.Development);
        Assert.Null(ex); // dev degrades, does not refuse boot
    }
}

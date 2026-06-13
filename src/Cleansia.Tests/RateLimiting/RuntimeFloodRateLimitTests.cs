using Cleansia.Tests.RateLimiting.Harness;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;

namespace Cleansia.Tests.RateLimiting;

/// <summary>
/// ADR-0003 — the RUNTIME 429 flood proof per policy CLASS, the behavioral complement to the
/// structural <see cref="RateLimitCoverageGuardTests"/>. That guard proves every money/side-effect
/// action *carries* a registered window (reflection over attributes); this floods one representative
/// per policy class through the REAL rate-limiter middleware (<see cref="RateLimiterHostHarness"/>,
/// TestServer — no Docker / JWT / Stripe) and asserts an ACTUAL HTTP 429 + Retry-After, plus that a
/// request UNDER the window still succeeds. Closes the T-0194 AC6 deviation (the runtime proof that
/// was deferred behind the structural guard).
///
/// Policy classes proven here:
///   - "auth" ANONYMOUS window (per real client IP) — flooded on a remediation-shaped anonymous
///     password path (the T-0194 remediation target), so AC2 proves the remediation attribute is
///     LIVE middleware, not dead metadata;
///   - "auth" AUTHENTICATED window (per JWT sub, looser limit);
///   - "webhook" per-source-IP window.
///
/// STALENESS GUARD (T-0193 account lockout): account lockout trips on repeated FAILED credential
/// checks (5/15min). It can mask a rate-limit 429 on a credentialed flood. These stub routes carry
/// NO credential check (the limiter is the only rejector), the password path is ANONYMOUS, and the
/// authenticated case uses a single distinct sub — so every 429 asserted here is the RATE LIMIT,
/// never the lockout.
/// </summary>
[Collection("RateLimiterHost")] // mutate process-wide limiter partitions; keep serial
public class RuntimeFloodRateLimitTests
{
    // Mirrors the shape of the T-0194 remediation targets Cleansia.Web.Customer.Controllers
    // .UserController.RequestPasswordChange / .ChangePassword: [AllowAnonymous] + the "auth" window.
    private const string RemediationPasswordPath = "/api/User/RequestPasswordChange";

    private static void MapRemediationPasswordRoute(IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(RemediationPasswordPath, (HttpContext c) =>
            {
                c.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            })
            .AllowAnonymous()              // mirrors the controller — anonymous, so NO lockout confound
            .RequireRateLimiting("auth");  // the genuine remediation policy

    // AC1 + AC2 — "auth" ANONYMOUS class on the remediation target. Flood the anonymous password
    // path from ONE client IP past the window: the over-limit POST is 429 with a positive Retry-After,
    // proving the remediation [EnableRateLimiting("auth")] is live middleware (AC2). A DISTINCT IP's
    // first POST in the same window still succeeds (a request under the window is served).
    [Fact]
    public async Task AC1_AC2_Auth_Anonymous_Remediation_Password_Path_Floods_To_429()
    {
        await using var h = await RateLimiterHostHarness.StartAsync(
            RateLimitTestConfig.Valid(authAnon: 5),
            extraEndpoints: MapRemediationPasswordRoute);

        const string floodIp = "203.0.113.40";
        for (var i = 1; i <= 5; i++)
        {
            var ok = await h.SendAsync(RemediationPasswordPath, xffClientIp: floodIp);
            Assert.False(ok.Is429, $"remediation password POST #{i} should be allowed under authAnon=5 (got {ok.StatusCode}).");
        }

        var rejected = await h.SendAsync(RemediationPasswordPath, xffClientIp: floodIp);
        Assert.True(rejected.Is429,
            "6th anonymous password POST over authAnon=5 must be a runtime 429 — the remediation window is live middleware.");
        Assert.False(string.IsNullOrEmpty(rejected.RetryAfter), "anonymous-auth 429 must carry Retry-After (D6).");
        Assert.True(int.TryParse(rejected.RetryAfter, out var secs) && secs > 0, "Retry-After must be a positive integer.");

        // A request UNDER the window (a fresh client IP = fresh partition) is still served.
        var freshClient = await h.SendAsync(RemediationPasswordPath, xffClientIp: "203.0.113.41");
        Assert.False(freshClient.Is429, "a distinct client IP under the window must still be served (not collateral-throttled).");
    }

    // AC1 — "auth" AUTHENTICATED class (per JWT sub, looser limit). Flood one sub past its window:
    // the over-limit POST is 429 with Retry-After. A SECOND sub from the same IP is unaffected,
    // proving the rejection is the sub's RATE LIMIT (not lockout, not an IP collision).
    [Fact]
    public async Task AC1_Auth_Authenticated_Sub_Floods_To_429()
    {
        await using var h = await RateLimiterHostHarness.StartAsync(
            RateLimitTestConfig.Valid(authAnon: 10, authAuthenticated: 4));

        const string sharedIp = "203.0.113.50";
        for (var i = 1; i <= 4; i++)
        {
            var ok = await h.SendAsync("/auth", xffClientIp: sharedIp, sub: "flooding-user");
            Assert.False(ok.Is429, $"authenticated auth POST #{i} should be allowed under authAuthenticated=4 (got {ok.StatusCode}).");
        }

        var rejected = await h.SendAsync("/auth", xffClientIp: sharedIp, sub: "flooding-user");
        Assert.True(rejected.Is429, "5th authenticated auth POST over authAuthenticated=4 must be a runtime 429.");
        Assert.False(string.IsNullOrEmpty(rejected.RetryAfter), "authenticated-auth 429 must carry Retry-After (D6).");
        Assert.True(int.TryParse(rejected.RetryAfter, out var secs) && secs > 0, "Retry-After must be a positive integer.");

        // A DIFFERENT sub on the SAME IP is under its own (untouched) window — so the 429 above was
        // the per-sub rate limit, not the IP window and not the T-0193 lockout.
        var otherSub = await h.SendAsync("/auth", xffClientIp: sharedIp, sub: "bystander-user");
        Assert.False(otherSub.Is429, "a distinct sub on the same IP must still be served — the rejection is per-sub rate limit, not lockout.");
    }

    // AC1 — "webhook" per-source-IP class. Flood the genuine webhook route from one source IP past
    // its window: the over-limit POST is 429 with Retry-After; a distinct source IP is still served.
    [Fact]
    public async Task AC1_Webhook_PerSourceIp_Floods_To_429()
    {
        await using var h = await RateLimiterHostHarness.StartAsync(RateLimitTestConfig.Valid(webhook: 5));

        const string sourceIp = "203.0.113.60";
        for (var i = 1; i <= 5; i++)
        {
            var ok = await h.SendAsync("/api/Payment/webhook", xffClientIp: sourceIp);
            Assert.False(ok.Is429, $"webhook POST #{i} should be allowed under webhook=5 (got {ok.StatusCode}).");
        }

        var rejected = await h.SendAsync("/api/Payment/webhook", xffClientIp: sourceIp);
        Assert.True(rejected.Is429, "6th webhook POST over webhook=5 must be a runtime 429.");
        Assert.False(string.IsNullOrEmpty(rejected.RetryAfter), "webhook 429 must carry Retry-After (D6/ADR-0004).");
        Assert.True(int.TryParse(rejected.RetryAfter, out var secs) && secs > 0, "Retry-After must be a positive integer.");

        var otherSourceIp = await h.SendAsync("/api/Payment/webhook", xffClientIp: "203.0.113.61");
        Assert.False(otherSourceIp.Is429, "a distinct source IP under the window must still be served (per-source-IP partition).");
    }
}

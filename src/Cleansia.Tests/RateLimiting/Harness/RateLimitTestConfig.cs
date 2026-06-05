namespace Cleansia.Tests.RateLimiting.Harness;

/// <summary>Builds the in-memory configuration the harness/host reads (ForwardedHeaders:* and
/// RateLimiting:*), mirroring the keys production reads. Small ceilings keep tests fast.</summary>
public static class RateLimitTestConfig
{
    /// <summary>A valid, narrow, non-dev-safe config trusting the loopback proxy peer.</summary>
    public static Dictionary<string, string?> Valid(
        int? authAnon = null,
        int? authAuthenticated = null,
        int? interactive = null,
        int? anonGlobalCeiling = null,
        int? webhook = null) => new()
    {
        ["ForwardedHeaders:ForwardLimit"] = "1",
        ["ForwardedHeaders:KnownProxies"] = RateLimiterHostHarness.TrustedProxyPeer, // 127.0.0.1
        ["ForwardedHeaders:KnownNetworks"] = "",
        ["RateLimiting:Auth:AnonPermitLimit"] = (authAnon ?? 10).ToString(),
        ["RateLimiting:Auth:AuthenticatedPermitLimit"] = (authAuthenticated ?? 30).ToString(),
        ["RateLimiting:Interactive:PermitLimit"] = (interactive ?? 60).ToString(),
        ["RateLimiting:Anon:GlobalCeiling"] = (anonGlobalCeiling ?? 10_000).ToString(),
        // SEC-W3 (T-0116) — the per-source-IP webhook window (config-overridable; small for tests).
        ["RateLimiting:Webhook:PermitLimit"] = (webhook ?? 60).ToString(),
    };
}

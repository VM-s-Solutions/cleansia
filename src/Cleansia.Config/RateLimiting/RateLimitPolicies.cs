using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using IPNetwork = System.Net.IPNetwork;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Cleansia.Config.RateLimiting;

/// <summary>
/// ADR-0003 (ADR-RATELIMIT) — the single, shared, partitioned rate-limiter contract for all five
/// hosts. Holds the partition-key functions (D2), the policy registration (D1), the global anonymous
/// cardinality cap (D7), the rejection behavior (D6), and the forwarded-headers trust boundary +
/// fail-closed startup guard (D3). <c>CleansiaStartupBase</c> calls into here; the policies are
/// defined ONCE and inherited identically — no host re-registers them (D5).
///
/// Partitioning principle: anonymous requests are isolated by the REAL client IP (the strongest
/// pre-auth server-derived identity, established by <see cref="ConfigureForwardedHeaders"/>);
/// authenticated requests by the JWT <c>sub</c>. Policy names <c>"auth"</c> / <c>"interactive"</c>
/// are preserved so existing <c>[EnableRateLimiting]</c> sites are untouched.
/// </summary>
public static class RateLimitPolicies
{
    public const string AuthPolicy = "auth";
    public const string InteractivePolicy = "interactive";

    // SEC-W3 (T-0116) — the Stripe-webhook per-source-IP window. A SEPARATE, third named policy
    // (ADR-0003 D5.2): a 429 to a webhook flood consumes none of the "auth"/"interactive" allowance
    // and vice-versa, because each named policy is its own partition tree.
    public const string WebhookPolicy = "webhook";

    // Config keys (all limits config-overridable for post-launch tuning — D6/D7).
    public const string AuthAnonLimitKey = "RateLimiting:Auth:AnonPermitLimit";
    public const string AuthAuthenticatedLimitKey = "RateLimiting:Auth:AuthenticatedPermitLimit";
    public const string InteractiveLimitKey = "RateLimiting:Interactive:PermitLimit";
    public const string AnonGlobalCeilingKey = "RateLimiting:Anon:GlobalCeiling";
    public const string WebhookLimitKey = "RateLimiting:Webhook:PermitLimit";
    public const string WebhookWindowSecondsKey = "RateLimiting:Webhook:WindowSeconds";

    // Defaults (D6 table). 10/min anon brute-force; 30/min authed (fits a real checkout); 60/min interactive.
    private const int DefaultAuthAnonLimit = 10;
    private const int DefaultAuthAuthenticatedLimit = 30;
    private const int DefaultInteractiveLimit = 60;
    private const int DefaultAnonGlobalCeiling = 20_000;
    // SEC-W3 (T-0116) — 60/min per source IP. Generous for a legitimate Stripe egress IP (events are
    // batched and re-driven, not flooded), tight enough to cap an anonymous DoS amplifier on the most
    // sensitive endpoint. Window defaults to the shared 60s; both are config-overridable (AC5/AC6).
    private const int DefaultWebhookLimit = 60;
    private const int DefaultWebhookWindowSeconds = 60;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    // ---- D2: partition-key functions (pure, unit-tested in isolation) --------------------------

    /// <summary>Anonymous → per real client IP (tight); authenticated → per <c>sub</c> (looser).
    /// Returns the namespaced partition key and whether the caller is anonymous.</summary>
    public static (string key, bool anonymous) AuthPartitionKey(HttpContext ctx)
    {
        if (TryGetSub(ctx, out var sub))
            return ($"auth:sub:{sub}", false);
        return ($"auth:ip:{ClientIp(ctx)}", true); // anonymous OR authed-without-sub (anomalous) → IP
    }

    public static (string key, bool anonymous) InteractivePartitionKey(HttpContext ctx) =>
        TryGetSub(ctx, out var sub)
            ? ($"interactive:sub:{sub}", false)
            : ($"interactive:ip:{ClientIp(ctx)}", true);

    /// <summary>SEC-W3 (T-0116) — the webhook is ALWAYS partitioned per SOURCE IP (the Stripe egress
    /// IP), never per <c>sub</c> (Stripe is unauthenticated — <c>[AllowAnonymous]</c> is preserved).
    /// Reuses the canonical <see cref="ClientIp"/> resolver verbatim (ADR-0003 D2/D5.2). AC6: we
    /// THROTTLE EVERY source IP and do NOT hard-reject unknown (non-Stripe-range) IPs — a stale Stripe
    /// allow-list would silently DROP REAL webhooks (lost payments) when Stripe rotates ranges; the
    /// signature check is the real auth, the rate limit is the DoS cap. Any published Stripe range is
    /// CONFIG-DRIVEN DOCUMENTATION only (env-injected, no hard-coded ranges); an optional future
    /// hard-reject lives behind a config toggle, default OFF.</summary>
    public static string WebhookPartitionKey(HttpContext ctx) => $"webhook:ip:{ClientIp(ctx)}";

    private static bool TryGetSub(HttpContext ctx, out string sub)
    {
        sub = string.Empty;
        if (ctx.User?.Identity?.IsAuthenticated == true
            && ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value is { Length: > 0 } s)
        {
            sub = s;
            return true;
        }
        return false;
    }

    /// <summary>Single canonical client-IP resolver. After <see cref="ConfigureForwardedHeaders"/>
    /// <c>RemoteIpAddress</c> IS the real client IP. A missing IP buckets into one shared "unknown"
    /// partition on purpose (deny-leaning). Also reused verbatim by SEC-W3's webhook policy (D5).</summary>
    public static string ClientIp(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    // ---- D1 + D6 + D7: register the limiter -----------------------------------------------------

    /// <summary>Registers the partitioned <c>"auth"</c>/<c>"interactive"</c> policies, the global
    /// anonymous cardinality cap (D7), and the rejection behavior (D6). Called once from
    /// <c>CleansiaStartupBase</c>.</summary>
    public static void AddCleansiaRateLimiter(IServiceCollection services, IConfiguration configuration)
    {
        var authAnonLimit = configuration.GetValue<int?>(AuthAnonLimitKey) ?? DefaultAuthAnonLimit;
        var authAuthedLimit = configuration.GetValue<int?>(AuthAuthenticatedLimitKey) ?? DefaultAuthAuthenticatedLimit;
        var interactiveLimit = configuration.GetValue<int?>(InteractiveLimitKey) ?? DefaultInteractiveLimit;
        var anonGlobalCeiling = configuration.GetValue<int?>(AnonGlobalCeilingKey) ?? DefaultAnonGlobalCeiling;
        // SEC-W3 (T-0116) — webhook permit + window, config-overridable (AC5/AC6).
        var webhookLimit = configuration.GetValue<int?>(WebhookLimitKey) ?? DefaultWebhookLimit;
        var webhookWindow = TimeSpan.FromSeconds(
            configuration.GetValue<int?>(WebhookWindowSecondsKey) ?? DefaultWebhookWindowSeconds);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests; // unchanged (:71)

            options.AddPolicy(AuthPolicy, ctx =>
            {
                var (key, anonymous) = AuthPartitionKey(ctx);
                var limit = anonymous ? authAnonLimit : authAuthedLimit;
                return FixedWindow(key, limit);
            });

            options.AddPolicy(InteractivePolicy, ctx =>
            {
                var (key, _) = InteractivePartitionKey(ctx);
                return FixedWindow(key, interactiveLimit);
            });

            // SEC-W3 (T-0116) — the THIRD named policy, per SOURCE IP, INDEPENDENT of auth/interactive.
            // Inherits the shared OnRejected / RejectionStatusCode (429) / Retry-After / QueueLimit=0
            // (it is a GetFixedWindowLimiter built by the same FixedWindow helper). A webhook flood to
            // 429 consumes none of the auth/interactive buckets and vice-versa — separate named policy =
            // separate partition tree (ADR-0003 D5.2 / verify #11).
            options.AddPolicy(WebhookPolicy, ctx =>
                FixedWindow(WebhookPartitionKey(ctx), webhookLimit, webhookWindow));

            // D7 — global anonymous cardinality cap. The GlobalLimiter runs for EVERY request in
            // addition to the per-endpoint policy and BOTH must grant. For anonymous requests it
            // acquires from one shared "anon-global" partition with a finite ceiling, so a spray of
            // millions of distinct IPs is rejected with 429 at the ceiling instead of minting an
            // unbounded number of live per-IP partitions (a memory-DoS strictly worse than the
            // rate-DoS we are fixing). Authenticated requests bypass the cap (bounded by the user
            // population + the /Register throttle).
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            {
                var anonymous = !TryGetSub(ctx, out _);
                if (!anonymous)
                    return RateLimitPartition.GetNoLimiter("authed-global");
                return RateLimitPartition.GetFixedWindowLimiter("anon-global", _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = anonGlobalCeiling,
                        Window = Window,
                        QueueLimit = 0,
                    });
            });

            options.OnRejected = OnRejected; // D6 — Retry-After (+ jitter); D8 — metric (policy name only)
        });
    }

    private static RateLimitPartition<string> FixedWindow(string key, int permitLimit, TimeSpan? window = null) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window ?? Window, // shared 60s unless a policy overrides (SEC-W3 webhook window)
            QueueLimit = 0, // reject immediately (D6) — never queue an auth/mutation
        });

    // ---- D6: rejection behavior -----------------------------------------------------------------

    private static readonly Random Jitter = Random.Shared;

    private static ValueTask OnRejected(OnRejectedContext context, CancellationToken cancellationToken)
    {
        // Prefer the lease's own RetryAfter (desyncs naturally per partition); else window + jitter
        // (0–15s) to avoid synchronizing every rejected client to the fixed-window boundary.
        var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? (int)retryAfter.TotalSeconds
            : (int)Window.TotalSeconds + Jitter.Next(0, 16);
        if (seconds <= 0) seconds = (int)Window.TotalSeconds;

        context.HttpContext.Response.Headers.RetryAfter =
            seconds.ToString(NumberFormatInfo.InvariantInfo);

        RateLimitMetrics.RecordRejection(context); // D8 — counter by POLICY NAME only (S6-safe)
        // S6: do NOT log the partition key here (it embeds the IP / sub). Nothing above Debug.
        return ValueTask.CompletedTask;
    }

    // ---- D3: forwarded-headers trust boundary + fail-closed startup guard -----------------------

    /// <summary>Configures <see cref="ForwardedHeadersOptions"/> from config (no hard-coded hop
    /// count) and registers the fail-closed validation that refuses to boot a non-Development host
    /// on an unset or over-broad trust config (D3 parts 2 &amp; 3).</summary>
    public static void ConfigureForwardedHeaders(
        IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.Configure<ForwardedHeadersOptions>(opts =>
        {
            opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            opts.ForwardLimit = configuration.GetValue<int?>("ForwardedHeaders:ForwardLimit") ?? 1;
            opts.KnownIPNetworks.Clear();
            opts.KnownProxies.Clear();
            foreach (var net in ParseKnownNetworks(configuration["ForwardedHeaders:KnownNetworks"]))
                opts.KnownIPNetworks.Add(net);
            foreach (var ip in ParseKnownProxies(configuration["ForwardedHeaders:KnownProxies"]))
                opts.KnownProxies.Add(ip);
        });

        var isDevelopment = environment.IsDevelopment();
        services.AddOptions<ForwardedHeadersOptions>()
            .Validate(o => ValidateForwardedHeadersConfig(o, isDevelopment),
                "ForwardedHeaders trust is unset or over-broad (ADR-0003 D3). In non-Development, set " +
                "ForwardedHeaders:KnownNetworks/KnownProxies to a NARROW ingress network (no /0–/8 supernet, " +
                "no 0.0.0.0/0 or ::/0). The app refuses to boot rather than silently run one global bucket.");

        // D8 #3 — in Development an empty config degrades to one bucket; emit the degraded signal so
        // a misconfigured non-prod environment is observable.
        if (isDevelopment && !HasTrustConfigured(configuration))
            RateLimitMetrics.SignalDegradedForwardedHeaders("development-unconfigured");
    }

    /// <summary>The fail-closed guard (pure, unit-tested). Non-Development refuses an empty trust
    /// config or any over-broad (/0–/8) network; Development always passes (degrades to one bucket).</summary>
    public static bool ValidateForwardedHeadersConfig(ForwardedHeadersOptions o, bool isDevelopment)
    {
        if (isDevelopment) return true; // dev may run without a proxy
        if (o.KnownIPNetworks.Count == 0 && o.KnownProxies.Count == 0) return false; // unset → refuse boot
        foreach (var n in o.KnownIPNetworks)
            if (n.PrefixLength <= 8) return false; // /0–/8 supernet (incl. 0.0.0.0/0, ::/0) → refuse boot
        return true;
    }

    private static bool HasTrustConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["ForwardedHeaders:KnownNetworks"])
        || !string.IsNullOrWhiteSpace(configuration["ForwardedHeaders:KnownProxies"]);

    /// <summary>Parses a comma/whitespace-separated CIDR list (e.g. "10.0.0.0/24, 169.254.0.0/16").</summary>
    public static IEnumerable<IPNetwork> ParseKnownNetworks(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) yield break;
        foreach (var token in Split(raw))
            if (IPNetwork.TryParse(token, out var net))
                yield return net;
    }

    /// <summary>Parses a comma/whitespace-separated IP list (e.g. "10.0.0.4, 10.0.0.5").</summary>
    public static IEnumerable<IPAddress> ParseKnownProxies(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) yield break;
        foreach (var token in Split(raw))
            if (IPAddress.TryParse(token, out var ip))
                yield return ip;
    }

    private static IEnumerable<string> Split(string raw) =>
        raw.Split(new[] { ',', ';', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

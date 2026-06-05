# ADR-0003 — Partitioned rate limiting: per-IP for anonymous auth, per-account for authenticated mutations, bounded cardinality, defined once and shared across all five hosts

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-01
- **Supersedes:** —
- **Superseded by:** —
- **Applies to:** backend | cross-cutting

> **Lead verdict (defense panel, 2026-06-01): CONSENSUS REACHED, zero blocking — `proposed → accepted`.**
> All 14 challenges (3 challengers) were conceded + fixed against re-verified code (config-driven
> `ForwardLimit` + fail-to-boot guard for Sec C1/C2; D7 cardinality cap for the memory-DoS; 30/min
> authenticated `auth` for the honest-checkout false-positive; `BSP-4d` for the coverage gap).
>
> **Owner answers (2026-06-01) — deploy gate now CLEARED:**
> - **Q-RATELIMIT-02 → CONFIRMED:** prod = App Service S1, **one trusted hop**, no Front Door/App
>   Gateway. Set `ForwardedHeaders:ForwardLimit = 1` + `KnownNetworks` = the narrow App Service ingress
>   CIDR; the D3 startup guard stays. **The rate-limit feature is cleared to enable in prod** with this
>   config.
> - **Q-RATELIMIT-03 → SHIP IT:** Wave 0 ships **per-IP-only**; `BSP-4b` (account/confirmation-code
>   lockout) is a **fast-follow, NOT an in-wave blocker**. Distributed code-guessing residual accepted
>   for launch (see Residual risk).
> - **Q-RATELIMIT-01 → default:** instances pinned to 1; scale-out >1 needs a distributed-limiter ADR first.

> This ADR is **ADR-RATELIMIT**. It is the frozen rate-limiter partitioning contract that BSP-4 /
> IDA-SEC-02 ship in **Wave 0** (story `US-partner-0042`). It decides the partition key, the named
> policies and their limits, the trusted-proxy/forwarded-headers stance, the partition-cardinality
> bound, the limit-hit behavior, the control's own observability, and the "defined once in
> `CleansiaStartupBase`, applied identically on all five hosts" shape. It does **not** fold in SEC-W3
> (the Stripe webhook per-IP window) — it makes the design *support* an independent per-IP webhook
> policy without coupling (D5). It does **not** add `[EnableRateLimiting]` to currently-uncovered
> money endpoints — that coverage gap is a named, tracked Wave-0 follow-up `BSP-4d` (see Consequences).
> Once `accepted` it is immutable — change it by superseding, never by editing.

---

## Context

`src/Cleansia.Config/Abstractions/CleansiaStartupBase.cs:69-93` registers the rate limiter as **two
named fixed-window limiters with no partition key**:

```csharp
options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;            // :71
options.AddFixedWindowLimiter("auth",        o => { o.PermitLimit = 10; o.Window = TimeSpan.FromMinutes(1); o.QueueLimit = 0; });  // :76-81
options.AddFixedWindowLimiter("interactive", o => { o.PermitLimit = 60; o.Window = TimeSpan.FromMinutes(1); o.QueueLimit = 0; });  // :87-92
```

A named limiter with **no partition** is **one global bucket shared by every caller** on that host.
`ASP.NET`'s `AddFixedWindowLimiter(name, …)` is sugar for a single un-partitioned window. Endpoints
opt in via `[EnableRateLimiting("auth")]` / `("interactive")` — class-level on `AuthController` across
all five hosts (e.g. `Web.Customer/Controllers/AuthController.cs`) and method-level on side-effecting
actions (`Web.Customer/Controllers/PaymentController.cs:16,32`, `GdprController`, `PromoCodeController`,
`ReferralController`, `OrderController.cs:40,51`). The limiter is defined **once** in the shared base
and inherited by all hosts (verified: all five hosts subclass `CleansiaStartupBase` and **none**
override `Configure` — they override only `AddProjectServices` and `UseHostAuthMiddleware`; e.g.
`Web.Customer/Startup.cs:40-43`). This single-definition shape is good and we keep it.

This is **two** verified, inseparable defects on the **same single decision** ("how the limiter
partitions callers"):

- **BSP-4 (major) / IDA-SEC-02 (critical) — no per-caller isolation.** Because the bucket is global:
  1. **Single-source brute-force is under-throttled and a trivial global DoS exists.** One client can
     spend all 10 `auth` permits/min on `/Login` and lock out **every** legitimate
     login/register/reset/refresh/confirm-email request on that host for the rest of each window. The
     inline comment "10/min — brute-force defense" is misleading: there is no per-IP or per-account
     isolation at all.
  2. **Distributed guessing is unmitigated** — see "Residual risk" below; this ADR fixes the
     single-source class, not the distributed class.

- **The proxy collapses any naive per-IP fix.** The codebase reads the client IP raw via
  `context.Connection.RemoteIpAddress` (`RequestMetadataProvider.cs:12`,
  `Web.Customer/Middleware/RequestLoggingMiddleware.cs:89`) and **there is no `UseForwardedHeaders`
  registered anywhere** (verified: zero matches in `src/`). Behind the App Service front end, every
  request's `RemoteIpAddress` is the **front-end/proxy** IP, so a per-IP partition keyed on
  `RemoteIpAddress` alone **collapses every caller into one partition** — re-creating the global
  bucket under a new name. The partition key and the forwarded-headers trust boundary are therefore
  **one decision**: you cannot ship per-IP partitioning correctly without also deciding which
  forwarded header is trusted, from whom, and with how many trusted hops.

**Production topology (verified, with a caveat).** `docs/architecture/infrastructure.md:16-21` lists
production as **App Service (Standard S1), one instance per API**, with **no Front Door, no Application
Gateway, and no other reverse proxy documented**. The only trusted hop is therefore the **App Service
built-in front end** (which appends one `X-Forwarded-For` entry and normalizes inbound XFF). The hop
count and the trusted network are made **config-driven** (not hard-coded) precisely because that doc
could be stale and the topology could change; the deployer confirms the live chain before flipping the
config (D3 + Q-RATELIMIT-02). Two facts from the same doc shape the rest of this ADR:
- **Single instance per API today, but Standard S1 supports autoscale** (`:16`). An in-process limiter
  is per-instance; scaling out silently multiplies the effective limit. We therefore **forbid
  scale-out until a distributed store ships** (D7/Consequences), rather than weaken the control exactly
  when load (and likely an attack) arrives.
- **App Insights with alert thresholds already exists** (`:266-279`) — so the control's own health
  metrics (D8) have a home.

The seam under pressure is the **"defined once in `CleansiaStartupBase`, identical on all five hosts"**
shape (CLAUDE.md: *Per-audience API hosts share Core + Infra + Config*). Whatever we choose must stay a
single definition that every host inherits, with the **anonymous-vs-authenticated partition selection
decided per request inside that one definition** — not copied per host (the exact failure mode ADR-0001
D4 fixed for authorization).

This is one ADR because splitting "partition key" from "forwarded-headers trust" from "cardinality
bound" from "shared-host shape" would let one half ship while another re-opens the hole (a per-IP key
with no trusted-proxy config is *worse* than today — it looks isolated but isn't; a per-IP key with no
cardinality bound trades a rate-DoS for a memory-DoS). **Endpoint *coverage* (which endpoints carry a
window) is a genuinely different decision and is deliberately *not* in this ADR — it is tracked as
`BSP-4d`.**

---

## Decision

> **Partitioning principle (governs all of D1–D8).** A rate-limit policy isolates **the smallest
> stable identity the request can be safely attributed to**: for an **anonymous** request that is the
> **real client IP** (the strongest server-derived identity available pre-auth); for an
> **authenticated** request it is the **caller's `sub`** (the JWT subject — stronger and more precise
> than IP, immune to NAT/CGNAT collisions and to IP rotation). The policy callback chooses **per
> request, inside the one shared definition**, based on whether `HttpContext.User` is authenticated.
> The window length stays 1 min and `429`/`QueueLimit=0` are preserved; **the per-partition *limit* is
> now chosen per partition kind** (anonymous-IP windows stay tight as a brute-force defense;
> authenticated-`sub` mutation windows are looser to fit a real session — D6). This is a hardening
> change with **no change for honest single callers** and a **bounded (≤2×) change for an adversary
> holding a valid account** (D2/Consequences).

### D1 — Replace the two named limiters with two **partitioned, cardinality-bounded** policies (same names)

The policy **names stay `"auth"` and `"interactive"`** so **no controller attribute changes** are
required (the existing `[EnableRateLimiting("auth"/"interactive")]` sites are untouched). Each named
limiter becomes a `PartitionedRateLimiter` via `options.AddPolicy(name, httpContext => …)`, and the
anonymous per-IP partition is **fronted by a coarse global cap** so distinct-IP cardinality cannot grow
without bound (D7). The shape (in `CleansiaStartupBase.ConfigureServices`, replacing lines 76-92):

```csharp
services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;   // unchanged (:71)

    // "auth" — credential / state-changing endpoints. Anonymous → per real client IP (tight,
    // brute-force defense). Authenticated → per sub (looser, sized for a real session). See D2/D6.
    options.AddPolicy("auth",        AuthLimiterFactory);
    // "interactive" — read-mostly / idempotent UI chatter. Same partition logic, looser limit.
    options.AddPolicy("interactive", InteractiveLimiterFactory);

    options.OnRejected = OnRejected;                          // D6 — Retry-After (+ jitter); D8 — metric
});
```

`AuthLimiterFactory` / `InteractiveLimiterFactory` build a `RateLimitPartition.GetFixedWindowLimiter`
whose **partition key** comes from D2 and whose **PermitLimit** comes from D6 (different number for the
IP partition vs the `sub` partition). The anonymous branch additionally participates in the global
cardinality cap of D7.

This **adapts** S5 (`agents/knowledge/security-rules.md §S5` — "Rate limiting on auth + side-effecting
endpoints"): S5 governs *which endpoints carry a window*; D1 fixes the *layer below it* so a window
actually isolates callers instead of pooling them. The attribute-placement audit (which endpoints get
`auth` vs `interactive`, and which uncovered money endpoints need one) is **out of scope of this ADR**
and tracked as **`BSP-4d`** (enumerated in Consequences).

### D2 — The partition-key functions (precise, per policy)

Both policies select the key **inside the one shared callback**, branching on authentication state, so
the anonymous-vs-authenticated choice is made centrally and identically on every host. Keys are
**prefixed by policy + key-kind** so the partition spaces never alias.

```csharp
// Anonymous auth routes (login/register/reset/confirm) → per real client IP (tight window, D6).
// Authenticated auth-tagged mutations (payment, gdpr, promo) → per caller sub (looser window, D6).
private static (string key, bool anonymous) AuthPartition(HttpContext ctx)
{
    if (ctx.User?.Identity?.IsAuthenticated == true
        && ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value is { Length: > 0 } sub)
        return ($"auth:sub:{sub}", false);
    return ($"auth:ip:{ClientIp(ctx)}", true);   // anonymous OR authed-without-sub (anomalous) → IP
}

private static (string key, bool anonymous) InteractivePartition(HttpContext ctx) =>
    ctx.User?.Identity?.IsAuthenticated == true
        && ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value is { Length: > 0 } sub
        ? ($"interactive:sub:{sub}", false)
        : ($"interactive:ip:{ClientIp(ctx)}", true);

// Single canonical client-IP resolver. After UseForwardedHeaders (D3) RemoteIpAddress IS the
// real client IP. A missing/unknown IP buckets into one shared "unknown" partition on purpose
// (deny-leaning: an attacker who strips their source IP shares one tight window).
private static string ClientIp(HttpContext ctx) =>
    ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
```

**Key decisions, stated and defended:**

- **`auth` anonymous = per real client IP, tight (10/min).** Pre-login the caller is anonymous; IP is
  the only server-derived identity. Closes the **single-source** DoS/lockout (one flooder consumes
  only its own 10/min). It does **not** close *distributed* guessing — see "Residual risk."

- **`auth` authenticated = per `sub`, looser (30/min, D6).** Once the JWT is validated
  (`UseAuthentication` runs **before** `UseRateLimiter` after the D4 fix), the precise identity is the
  subject claim. Partitioning a payment/GDPR/promo mutation by `sub` throttles only that user and
  survives IP changes (mobile/Wi-Fi handoff).

- **Per-IP only, not IP+username, for `/Login`.** Considered keying login on `ip + attemptedEmail`.
  **Decided: per-IP only for v1.** Reasons: (a) the attempted username lives in the **request body**
  (`Login.Command.Email`), and the limiter callback runs **before** model binding — reading it requires
  buffering/parsing the body in the limiter (forbidden by the role's "does NOT know" list); (b) an
  attacker controls the body, so an IP+email key lets an attacker **multiply their own permits** by
  varying the email — it can *weaken* IP isolation; (c) the correct per-account control is
  **account-lockout-after-N-failures**, deferred to `BSP-4b` (Residual risk). The chosen key does not
  preclude it — a future ADR can add a *bound* attempted-account dimension.

- **Token-validity determines IP-vs-`sub` (a bounded, accepted asymmetry).** Because the branch reads
  `HttpContext.User`, a caller who *has* a valid token keys on `sub` while the *same box* sending
  no-token requests keys on IP. An adversary holding one valid account thus gets `sub:X` **plus**
  `ip:theirs` on the `auth` policy ≈ **2× the bound** on mixed-auth controllers. This is **accepted for
  v1**: it is still vastly better than one global bucket, and *how many* valid accounts one IP can mint
  is bounded by the `/Register` throttle (anonymous, IP-partitioned by this ADR → ~10 registrations/
  min/IP) and addressed further by `BSP-4b`. Recorded in Consequences, not hidden.

- **Keys are namespaced (`auth:`/`interactive:`, `ip:`/`sub:`).** Disjoint partition spaces; a `sub`
  that equals an IP string can never share a window.

### D3 — Forwarded-headers trust boundary (config-driven, fail-to-start on misconfig)

Per-IP partitioning is **meaningless without** establishing the real client IP behind the App Service
front end. The fix has three non-negotiable parts:

1. **Register `UseForwardedHeaders` at the very top of `Configure` — before the request-logging
   middleware** (so both the limiter *and* the audit-log IP read the real client IP). Insert in
   `CleansiaStartupBase.Configure` immediately after the `EnableBuffering` `Use(...)` block and
   **before** `app.UseMiddleware(RequestLoggingMiddlewareType)` (currently `:120`):
   ```csharp
   app.UseForwardedHeaders();   // MUST precede RequestLoggingMiddleware (:120) AND UseRateLimiter
   ```

2. **Options are config-driven** (no hard-coded hop count) and **validated at startup**:
   ```csharp
   services.Configure<ForwardedHeadersOptions>(opts =>
   {
       opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
       opts.ForwardLimit = Configuration.GetValue<int?>("ForwardedHeaders:ForwardLimit") ?? 1; // App-Service front end = 1 trusted hop (documented default; confirm per env — Q-RATELIMIT-02)
       opts.KnownProxies.Clear();
       opts.KnownNetworks.Clear();
       foreach (var net in ParseKnownNetworks(Configuration["ForwardedHeaders:KnownNetworks"]))
           opts.KnownNetworks.Add(net);
       foreach (var ip in ParseKnownProxies(Configuration["ForwardedHeaders:KnownProxies"]))
           opts.KnownProxies.Add(ip);
   });
   services.AddOptions<ForwardedHeadersOptions>().Validate(ValidateForwardedHeadersConfig); // see part 3
   ```

3. **Startup guard — fail fast on BOTH misconfigurations (unset AND over-broad).** A degraded
   brute-force defense is a disabled security control nobody notices; it must be loud, not silent. In
   **non-Development** environments the app **must fail to start** if any of:
   - `KnownNetworks` AND `KnownProxies` are both empty/unset, OR
   - any `KnownNetworks` prefix has length **`/0`–`/8`** (a supernet — too broad to trust), OR
   - `KnownNetworks` contains `0.0.0.0/0` or `::/0`.
   ```csharp
   private bool ValidateForwardedHeadersConfig(ForwardedHeadersOptions o)
   {
       if (Environment.IsDevelopment()) return true;            // dev may run without a proxy
       if (o.KnownNetworks.Count == 0 && o.KnownProxies.Count == 0) return false; // unset → refuse boot
       foreach (var n in o.KnownNetworks)
           if (n.PrefixLength <= 8) return false;               // /0–/8 supernet → refuse boot
       return true;
   }
   ```
   In **Development**, an empty config is allowed and the control degrades to one bucket; this is the
   only environment where degrade-to-one-bucket is acceptable, and D8 emits a "degraded" signal so it
   is observable.

**Which header, and is it spoofable?** We honor **`X-Forwarded-For`** and **only** from a **trusted**
peer. The middleware walks XFF right-to-left and replaces `RemoteIpAddress` only for hops whose
immediate peer is in `KnownProxies`/`KnownNetworks`, limited to `ForwardLimit`. **Spoofability answer:**
a client can send an arbitrary XFF, but because the real connection's immediate peer is the trusted
front end and `ForwardLimit` equals the trusted-hop count, a client-supplied *leftmost* XFF entry is
not honored — we read the one entry the *trusted* front end appended. The **danger case** is an
over-broad `KnownNetworks` that trusts an attacker-reachable peer — which is why the startup guard
**refuses to boot** rather than silently trusting it. **Verification #4** asserts: distinct real client
IPs → distinct partitions; spoofed XFF from an untrusted hop → ignored; over-broad `KnownNetworks` →
the guard refuses to boot.

**Infra obligation (MANUAL_STEP, blocking deploy gate — not a deploy nicety):**
`ForwardedHeaders:KnownNetworks`/`KnownProxies` and `ForwardedHeaders:ForwardLimit` are
environment-specific and **gate whether the feature works at all** (unset/wrong = no-op or fail-open).
They are injected via env/config (no secret). The deployer **confirms the live ingress chain and which
appliance strips inbound XFF** before setting them (Q-RATELIMIT-02). The startup guard makes a wrong/
missing value a **boot failure**, not a silent degrade, so the `AddPolicy` grep + synthetic tests can
never pass while production runs one-bucket.

### D4 — Middleware ordering (a real bug the partition depends on) — complete order, CSRF included

The authenticated key reads `HttpContext.User`; the per-IP key reads the **forwarded**
`RemoteIpAddress`; the audit log also reads the IP. All require the right pipeline order.

**Complete current order** (`CleansiaStartupBase.Configure`):
```
… EnableBuffering(:104-108) → [Swagger if !prod] → UseMiddleware(RequestLogging)(:120)
  → UseExceptionHandler(:122) → UseRouting(:130) → UseCors(:131) → UseRateLimiter(:132)
  → UseAuthentication(:133) → UseHostAuthMiddleware(:137, CSRF on web hosts) → UseAuthorization(:138) → endpoints
```
Two bugs: **(i)** `UseRateLimiter` (`:132`) runs **before** `UseAuthentication` (`:133`), so
`HttpContext.User` is unpopulated and the `sub` branch could never fire; **(ii)** there is no
`UseForwardedHeaders`, so the IP is the front-end IP for both the limiter and the audit log.

**Complete target order:**
```
… EnableBuffering → UseForwardedHeaders(NEW, at top) → [Swagger if !prod] → UseMiddleware(RequestLogging)
  → UseExceptionHandler → UseRouting → UseCors → UseAuthentication → UseRateLimiter
  → UseHostAuthMiddleware (CSRF, UNCHANGED position) → UseAuthorization → endpoints
```
Decisions:
- `UseForwardedHeaders` moves to the **top** (before `RequestLogging` at `:120`) so the **audit-log IP
  is also corrected** (`RequestMetadataProvider.IpAddress` / `RequestLoggingMiddleware.cs:89`), not
  just the limiter. (This is now **in scope** — the earlier draft's "audit-log IP out of scope" caveat
  is removed.)
- `UseRateLimiter` moves to run **after** `UseAuthentication` so `User` is populated for the `sub`
  branch.
- **`UseHostAuthMiddleware` (CSRF validation on Customer/Partner/Admin; no-op on Mobile, per
  `CleansiaStartupBase.cs:27-32,134-137` and `Web.Customer/Startup.cs:40-43`) stays exactly where it
  is — between `UseAuthentication`/`UseRateLimiter` and `UseAuthorization`, unchanged.** This ADR
  rewrites the pipeline band that contains the CSRF hop, so the hop is named explicitly to prevent an
  implementer from dropping or reordering it. CSRF still runs after the limiter (as today).

This ordering lives in the shared base — fixed once for all five hosts.

### D5 — Shared-across-hosts shape (the ADR mandate) + SEC-W3 independence

1. **One definition, five hosts, zero per-host edits.** The policies, partition-key functions,
   `ForwardedHeadersOptions` + guard, the cardinality cap (D7), and the pipeline order all live **only**
   in `CleansiaStartupBase` (`ConfigureServices` + `Configure`). All five hosts (`Web.Admin`,
   `Web.Partner`, `Web.Customer`, `Web.Mobile.Partner`, `Web.Mobile.Customer`) inherit it — verified
   none override `Configure`. A host must not re-register `"auth"`/`"interactive"` or call
   `AddRateLimiter` (verification #6, by explicit host name).

2. **SEC-W3 (webhook per-IP window) stays independent — design supports it, ADR does not fold it in.**
   The Stripe webhook (`Web.Customer/Controllers/PaymentController.cs:43-47` and Partner/Mobile.Customer
   equivalents) is `[AllowAnonymous]` with **no** limiter today. SEC-W3 will add a **separate, third
   named policy** (`"webhook"`) partitioned **per source IP** (Stripe egress ranges) applied via
   `[EnableRateLimiting("webhook")]` on the webhook actions only. This ADR guarantees the machine
   supports that without coupling: `AddPolicy` allows N independent named policies, the `ClientIp(ctx)`
   resolver and the D3 forwarded-headers fix are reusable verbatim, and the webhook policy uses its own
   limit/window/key without touching `"auth"`/`"interactive"`. **SEC-W3 owns the webhook limit + the
   Stripe-IP allow-list; this ADR owns only the shared partitioning shape it slots into.** Webhooks
   stay *outside* `"auth"`/`"interactive"` (a 429 to Stripe looks like a 5xx-class retry trigger).

### D6 — Behavior on limit hit: `429`, `QueueLimit=0`, per-partition limits, `Retry-After` + jitter

- **Per-partition limits (re-justified, not inherited).** `10/min` was chosen for a *global*
  brute-force bucket (`CleansiaStartupBase.cs:72-75`). A per-`sub` authenticated-mutation window is a
  different control needing a different number. The limits, all config-overridable for post-launch
  tuning:

  | Policy | Anonymous (IP) partition | Authenticated (`sub`) partition | Config key |
  |---|---|---|---|
  | `auth` | **10/min** (brute-force defense — unchanged) | **30/min** (sized above the P99 of a real checkout-with-card-retries session) | `RateLimiting:Auth:{Anon,Authenticated}PermitLimit` |
  | `interactive` | **60/min** (unchanged) | **60/min** | `RateLimiting:Interactive:PermitLimit` |

  Rationale for 30 on authenticated `auth`: a real customer doing create-order → 2-3 declined-card
  retries (routine) → re-quote → confirm can exceed 10 mutating calls in 60s on the `auth`-tagged
  `OrderController`/`PaymentController.CreatePaymentIntent:31`. With `QueueLimit=0` the old 10 would
  hard-reject mid-checkout (a revenue event). 30 covers it with margin; verification #9 proves a real
  session stays under.

- **Keep `QueueLimit = 0` (reject immediately).** Queueing a rejected auth/mutation holds a
  brute-force attempt open and adds latency for no benefit; fail-fast is correct. Preserved.
- **Keep `429` (`RejectionStatusCode` unchanged, `:71`).**
- **`Retry-After` with jitter (anti-thundering-herd).** On a *fixed* window, a flat `Retry-After: 60`
  synchronizes every rejected client to the window boundary, and SPAs/mobile honoring it re-spike at
  rollover. So: prefer the lease's actual `RetryAfter` metadata (differs per partition, desyncs
  naturally); only when absent, fall back to **window length + small random jitter**:
  ```csharp
  private static readonly Random Jitter = Random.Shared;
  private static ValueTask OnRejected(OnRejectedContext ctx, CancellationToken _)
  {
      var seconds = ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ra)
          ? (int)ra.TotalSeconds
          : 60 + Jitter.Next(0, 16);                    // jitter 0–15s to desync herd
      ctx.HttpContext.Response.Headers.RetryAfter =
          seconds.ToString(NumberFormatInfo.InvariantInfo);
      RateLimitMetrics.RecordRejection(ctx);            // D8 — counter by POLICY NAME only (S6-safe)
      // S6: do NOT log the partition key above Debug (it contains IP / sub).
      return ValueTask.CompletedTask;
  }
  ```
  A client-side back-off-jitter follow-up for the SPA/mobile clients is filed as `BSP-4c`.
- **S6 note:** the partition key embeds the client IP or `sub` (PII-adjacent) and must **never** be
  logged above `Debug`. The D8 metric is dimensioned by **policy name**, never by key.

### D7 — Partition cardinality bound (availability — a botnet of *real* IPs must not OOM the host)

.NET's `PartitionedRateLimiter` does **not** evict idle partitions on a timer; it disposes a
partition's limiter only opportunistically. So a spray of millions of **distinct real source IPs**
against anonymous `/Auth/Login` would mint millions of live `FixedWindowRateLimiter` objects on a
**single S1 instance** (`infrastructure.md:16`) — converting the self-resetting 60s rate-DoS into a
**non-resetting memory-DoS / OOM**, which is *strictly worse* than the bug we are fixing.
"Fail-closed to one bucket" (D3) guards *spoofed* XFF, not a botnet of valid distinct IPs.

Decision: the **anonymous (IP) partitions of both policies are fronted by a coarse global ceiling.**
The anonymous branch first acquires from a single **global "anonymous" partition** (a fixed-window
limiter with a high but finite ceiling, sized so total simultaneous distinct anonymous partitions
cannot grow without bound); only if that global lease is granted does the request consume its per-IP
window. When the global ceiling is saturated, *new* anonymous requests are rejected with `429` instead
of allocating a fresh partition object. Concretely, the anonymous limiter is a `PartitionedRateLimiter`
whose factory chains a per-IP fixed window behind a shared global fixed window (one global partition
key), so the live-partition count is bounded by the global ceiling × the window.

- Authenticated `sub` partitions are intrinsically bounded by the (registered) user population and
  gated upstream by the `/Register` throttle (D2); they carry a documented **soft ceiling** but do not
  need the coarse anonymous cap.
- The **global ceiling is config-driven** (`RateLimiting:Anon:GlobalCeiling`, default sized for S1).
- D8's **partition-count gauge** makes cardinality growth observable *before* OOM.

This keeps the Consequences claim ("one flooder can no longer lock out the platform") **true**: a
flooder can neither exhaust everyone's window (per-IP isolation) nor OOM the box (global cap).

### D8 — Observability of the control's own health (you can't operate what you can't see)

This ADR introduces three silent failure modes — (1) `KnownNetworks` degrade (now a boot failure in
prod, D3, but Development can still degrade), (2) per-instance scale-out doubling (D7/Consequences),
(3) partition-cardinality growth (D7). The control must be observable. Mandated, all S6-safe and
feeding the existing App Insights alerts (`infrastructure.md:266-279`):

1. **`429`-rejection counter dimensioned by POLICY NAME** (`auth`/`interactive`), never by key —
   emitted from `OnRejected` (D6). Alert on a sustained spike.
2. **Partition-count / `GetStatistics` gauge** per policy — the Challenge-A early warning; alert when
   it approaches the D7 global ceiling.
3. **"Degraded to single bucket" signal** — emitted when `ValidateForwardedHeadersConfig` is bypassed
   (Development) or when `RemoteIpAddress` resolves to the configured proxy network at runtime; surfaces
   a misconfigured non-prod environment and any future prod regression.

These are added to the `infrastructure.md` "Key Metrics Monitored" table (same change).

---

## Residual risk (explicitly UNMITIGATED by this ADR)

This ADR closes the **single-source** DoS/lockout and per-attacker brute-force class. It does **not**
close the following — stated plainly so no reader concludes they are handled:

- **Distributed credential-stuffing.** A botnet of 10,000 IPs × 10/min = 100,000 guesses/min against
  one account; per-IP partitioning cannot touch it (each bot stays under its own window). Mitigated
  only by **account-lockout-after-N-failures** → **`BSP-4b` (blocking companion)**.
- **Distributed confirmation-code guessing (worse than passwords).** Verified: `ConfirmUserEmail`
  looks up by **code alone** with no account binding (`ConfirmUserEmail.Command(user.ConfirmationCode)`;
  `UserRepository` `GetByConfirmationCode*`/`ExistsWithConfirmationCodeAsync`), and the endpoint is
  `[AllowAnonymous]`. A 6-digit code is a 10^6 space; distributed across a botnet it is brute-forceable
  in hours. Per-`sub` cannot apply (anonymous); per-IP cannot stop distribution. The single most
  exposed surface is the one this ADR's partition key protects least. Mitigated only by a **per-code
  attempt counter / account-bound code lookup + throttle** → **`BSP-4b` (blocking companion)**.
  **Whether Wave 0 may ship with this surface unmitigated is an owner decision → Q-RATELIMIT-03.**
- **Account-farming for a second `auth` bucket (≤2×).** One IP minting N self-serve accounts gets
  ~N×(authenticated bound) on the authed surface, and ~2× on mixed-auth controllers (D2). Bounded today
  by the `/Register` IP throttle (~10 registrations/min/IP) and further addressed by `BSP-4b`.
- **Cross-instance correctness if the API is ever scaled out.** In-process limiter is per-replica →
  N replicas ≈ N× the limit. **Scale-out is forbidden until the distributed limiter ships**
  (Consequences / Q-RATELIMIT-01).

---

## Alternatives considered

- **Keep the two un-partitioned named limiters; just lower the limits.** Rejected: tightening 10/min
  makes the **global DoS worse** while still not isolating an attacker. The defect is the *missing
  partition*, not the number.
- **Per-IP for everything (including authenticated mutations).** Rejected: NAT/CGNAT puts many
  legitimate users behind one IP — they'd throttle each other on payment/GDPR. Authenticated callers
  have a stronger identity (`sub`).
- **Per-account (`sub`) for everything, including login.** Impossible pre-login (no `sub`).
- **Compound `sub` AND IP key for authenticated callers.** Rejected for v1: reintroduces NAT
  collisions for the authed surface (the thing per-`sub` fixes) and **doubles partition cardinality**
  (feeds the D7 memory-DoS). The ≤2× account-farming residual (D2) is instead bounded by the `/Register`
  throttle + `BSP-4b`.
- **`ip + attemptedUsername` key on `/Login`.** Rejected for v1 (full reasoning in D2): unbound
  attacker-controlled body; correct control is account-lockout (`BSP-4b`).
- **Hard-code `ForwardLimit = 1`.** Rejected (challenge): the production proxy chain is documented only
  by omission (`infrastructure.md:16-21` shows no Front Door/AppGW), so the hop count is config-driven
  with a documented default of 1 and a deployer confirmation step (Q-RATELIMIT-02).
- **Unbounded per-IP partitions (no cardinality cap).** Rejected (challenge): trades a rate-DoS for a
  memory-DoS / OOM on the single S1 instance. See D7.
- **Distributed (Redis-backed) limiter for cross-instance correctness.** Deferred, not rejected.
  Acceptable for Wave 0 **only because** scale-out is forbidden until it ships (Consequences). Trigger:
  Q-RATELIMIT-01 fires when instance-count > 1 is *requested* (not after it happens).
- **Do per-IP rate limiting at the Azure ingress/WAF instead of in-app.** Complementary defense-in-
  depth, but the ingress can't cheaply see the validated JWT `sub` (no per-`sub` partition) and it
  couples the control to infra outside this repo's CI. Keep the in-app limiter as source of truth.
- **Fold attribute *coverage* (uncovered money endpoints) into this ADR.** Rejected: coverage (which
  endpoints carry a window) is a different decision from partitioning (how a window isolates callers) —
  one ADR = one decision. Tracked as `BSP-4d`, enumerated in Consequences.

---

## Consequences

**Cheaper:**
- One flooder can no longer lock out the platform *and* can no longer OOM it: anonymous auth routes are
  isolated per real client IP behind a global cardinality cap; authenticated mutations per `sub`. The
  single-source global-DoS class (IDA-SEC-02 critical) is structurally removed.
- One **shared definition** — every host gets identical, correct partitioning with no per-host edits,
  and the machine cleanly accepts SEC-W3's independent webhook policy and any future per-user limit
  without re-architecture.
- The audit-log IP is fixed for free (D4 moves `UseForwardedHeaders` above `RequestLogging`).
- **No controller changes, no DTO/NSwag change, no EF migration** — policy names are preserved, so the
  existing `[EnableRateLimiting]` sites are untouched.

**More expensive (new obligations):**
- **Blocking infra/deploy gate:** `ForwardedHeaders:KnownNetworks`/`KnownProxies`/`ForwardLimit` MUST
  be set per environment to a **narrow** ingress network. Unset OR over-broad (`/0`–`/8`) → the app
  **fails to start** in non-dev (D3 guard). Confirm the live chain first (Q-RATELIMIT-02).
- **Scale-out forbidden until the distributed limiter ships.** API App Service instance count is
  **pinned to 1** (deploy checklist + MANUAL_STEP). A scale-out request triggers the superseding ADR
  (Q-RATELIMIT-01). Rationale: an in-process limit halves per replica — do not weaken the control when
  scaling to absorb load.
- **Pipeline order is now load-bearing:** `UseForwardedHeaders` at the top; `UseRateLimiter` **after**
  `UseAuthentication`; CSRF (`UseHostAuthMiddleware`) **unchanged** between them and `UseAuthorization`.
  Verification #5 guards all three.
- **Observability is mandatory** (D8): rejection counter by policy name, partition-count gauge,
  degraded-mode signal — wired to App Insights.
- **S6:** the partition key is PII-adjacent and must never be logged above `Debug`; the metric is by
  policy name only.

**Residual (accepted for v1, see "Residual risk"):** distributed stuffing, distributed confirmation-
code guessing, ≤2× account-farming, per-instance scope — all named with their dependent control
(`BSP-4b`) and owner question (Q-RATELIMIT-03).

**Tracked Wave-0 follow-ups (not this ADR, but named so the gap is a decision, not an omission):**
- **`BSP-4b` (blocking companion):** account-lockout / per-confirmation-code attempt throttle + the
  `/Register` account-farming bound. Owner-gated by Q-RATELIMIT-03.
- **`BSP-4c` (non-blocking):** SPA/mobile client-side `Retry-After` back-off jitter.
- **`BSP-4d` (attribute coverage):** add `[EnableRateLimiting]` to currently-uncovered money/side-
  effect endpoints — verified missing: `Web.Customer/MembershipController.cs` `CreateCheckoutSession:46`
  (Stripe checkout — money), `Subscribe:15`, `Cancel:27`, `SwapPlan:67`; `DisputeController`
  `Create/UploadEvidence/AddMessage`; `RecurringBookingController` `Create/Update/SetActive/Delete`;
  `DeviceController` `Register`; `Web.Partner` `PaymentController`/`PayPeriodController`/
  `PayConfigController`/`EmployeePayrollController`/`DisputeController`. (SEC-W3 owns the webhook.)

**Blast radius at deploy:**
- Honest single callers: **no change** (own per-partition allowance — strictly more permissive than the
  shared pool they were in).
- An adversary holding a valid account: **bounded ≤2×** on mixed-auth `auth` controllers (D2),
  bounded further by the `/Register` throttle — still vastly better than one global bucket.
- Misconfigured `KnownNetworks` at first deploy: **app refuses to start** in non-dev (loud, not a
  silent no-op) — the deployer sets the correct narrow value and redeploys.
- SEC-W3 ships separately; this ADR must land **first** (provides `UseForwardedHeaders` + `ClientIp`).

**Rollout = three deliverables (not one ticket):**
1. **(this ADR's PR)** partition policies + partition-key functions + `UseForwardedHeaders` & guard +
   D7 cardinality cap + D6 limits + D8 metrics, all in `Cleansia.Config` (shared base) — **plus a new
   `WebApplicationFactory`-style integration harness** (verified: the current `Cleansia.IntegrationTests`
   harness — `BaseIntegrationTest`/`PostgresContainerFixture`, e.g. `ConfirmUserEmailTests.cs` — is
   MediatR/DbContext-level and **cannot** boot a host with `ForwardedHeaders` config to exercise the
   limiter middleware; building that harness is sized as part of this deliverable).
2. **(infra)** set `ForwardedHeaders:*` per environment + pin instance count to 1 — **blocking deploy
   gate**, enforced by the D3 startup guard.
3. **(`BSP-4d`)** attribute-coverage follow-up (enumerated above).
- No EF migration, no NSwag regen, no controller attribute edits in deliverable 1.

---

## How a reviewer verifies compliance

**Mechanical (automated — these are the gate):**

1. **No un-partitioned limiter remains.** `AddFixedWindowLimiter(` must not appear in any `*Startup*.cs`
   (or anywhere); the limiter is registered via `options.AddPolicy("auth"/"interactive", …)` with
   `RateLimitPartition.GetFixedWindowLimiter`. File a `check-consistency.mjs` rule
   (`process/enforcement.md`): fail if `AddFixedWindowLimiter(` exists in any `*Startup*.cs`.

2. **Partition isolation (core integration test — requires the new host harness).** Boot a host through
   the real pipeline (forwarded-headers configured for the test network): client A (IP `10.0.0.1`)
   sends 10 `POST /Auth/Login` then 1 more → A's 11th is `429`; client B (IP `10.0.0.2`) first request
   in the same window → **not** `429`. Fails against current single-bucket code, passes after the fix
   (AC-1/AC-2/AC-6 of `US-partner-0042`).

3. **Per-`sub` isolation test.** Two authenticated users on an authenticated `auth`-tagged endpoint:
   user X exhausts X's window → only X is `429`; user Y (different `sub`, **same** IP) unaffected.

4. **Forwarded-headers / spoof / over-broad-trust test.** With a **narrow** `KnownNetworks`: distinct
   trusted-proxy `X-Forwarded-For` client IPs → distinct partitions; a spoofed XFF from an **untrusted**
   hop → ignored (keyed by connection IP). With an **over-broad** `KnownNetworks` (`0.0.0.0/0` or a
   `/8`): the **startup guard refuses to boot** (assert it throws). With **empty** `KnownNetworks` in a
   non-dev environment: refuses to boot. Proves D3.

5. **Pipeline-order test (complete band, incl. CSRF).** Assert in `Configure`: `UseForwardedHeaders`
   precedes `RequestLogging` and `UseRateLimiter`; `UseRateLimiter` runs **after** `UseAuthentication`;
   **`UseHostAuthMiddleware` (CSRF) stays between `UseRateLimiter`/`UseAuthentication` and
   `UseAuthorization`.** A test that an authenticated request is keyed by `sub` not IP doubles as the
   auth-order guard.

6. **Single-definition / no-per-host-override check (by explicit host name).** Grep all five host
   projects — `Web.Admin`, `Web.Partner`, `Web.Customer`, `Web.Mobile.Partner`, `Web.Mobile.Customer`:
   **zero** calls to `AddRateLimiter`, `AddFixedWindowLimiter`, or `AddPolicy("auth"/"interactive")`
   outside `CleansiaStartupBase`. (Keep the literal name list so a future sixth host can't be skipped.)

7. **Limits/behavior preserved + per-partition split.** `auth` IP partition = 10/min, `auth` `sub`
   partition = 30/min, `interactive` = 60/min; `Window = 1 min`, `QueueLimit = 0`,
   `RejectionStatusCode == 429`; rejected response carries `Retry-After` (D6).

8. **Cardinality bound (D7).** A test spraying many distinct synthetic client IPs (via trusted XFF)
   does **not** grow live partitions without bound — the global anonymous ceiling rejects with `429`
   past the cap; the partition-count gauge stays bounded.

9. **False-positive / honest-session test (D6).** A scripted realistic authenticated checkout
   (create-order + 3 declined-card retries + re-quote + confirm ≈ 8 mutations in <60s) completes
   with **no** `429`. Proves the per-`sub` `auth` limit (30) doesn't break legitimate checkout.

10. **Observability (D8).** A rejection emits the policy-name-dimensioned counter (no key in the
    metric); the partition-count gauge is exported; a degraded-mode signal fires when forwarded-headers
    are unconfigured (dev).

11. **SEC-W3 independence (when it lands).** The webhook policy is a separate named policy keyed by
    source IP; flooding it consumes no `"auth"`/`"interactive"` allowance and vice-versa. (Listed so the
    SEC-W3 reviewer inherits the contract; not gating this ADR.)

**Manual checklist (blocking deploy gate):**
- `ForwardedHeaders:KnownNetworks`/`KnownProxies`/`ForwardLimit` set per environment to a **narrow**
  ingress network; the startup guard boots successfully (proves they're set and not over-broad).
- API App Service instance count **pinned to 1** (no autoscale) until the distributed limiter ships.
- The partition key is not logged above `Debug` (S6); metrics are by policy name only.
- Q-RATELIMIT-02 (topology/hop-count) confirmed; Q-RATELIMIT-03 (confirmation-code ship decision)
  answered by the owner before flipping the feature on.

---

## Roles affected

New role file in `agents/knowledge/roles/`:

- **`rate-limit-policy.md`** — CRC card (created this change). Responsibility: decide, once and
  identically for all five hosts, which **bounded** partition a request is rate-limited under (real
  client IP for anonymous, JWT `sub` for authenticated) and reject excess with `429` + `Retry-After`.
  Its "does NOT know" list (request body, webhook IP ranges, lockout state, per-host logic) is what
  makes D2's rejection of body-derived login keys a **structural** rule. Updated this change to add the
  cardinality-bound and observability collaborators.

Catalog edit (same change): `agents/knowledge/security-rules.md §S5` already records that auth/side-
effecting windows MUST be **partitioned** per ADR-0003 (an un-partitioned named limiter is an S5
violation). Amended further this change to note the **cardinality bound** and the **coverage follow-up
`BSP-4d`** (a partitioned-but-uncovered money endpoint is still an S5 gap).

Questions opened (same change): `agents/backlog/questions/open.md` —
- **Q-RATELIMIT-01** — distributed limiter trigger (fires when API instance-count > 1 is requested).
- **Q-RATELIMIT-02** — confirm the production proxy chain / hop count / which appliance strips XFF.
- **Q-RATELIMIT-03 (owner, blocking)** — may Wave 0 ship with the confirmation-code brute-force surface
  unmitigated (per-IP only), or must the `BSP-4b` per-code throttle land in the same wave?

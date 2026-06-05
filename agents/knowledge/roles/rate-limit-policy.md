# Role — Rate-Limit Policy (CRC card)

> Introduced by ADR-0003 (ADR-RATELIMIT). Lives in `CleansiaStartupBase` as the partitioned
> `"auth"` / `"interactive"` policies and their partition-key functions.

- **Name:** Rate-Limit Policy

- **Responsibility (one sentence):** Decide, once and identically for all five hosts, which **bounded**
  partition a request is rate-limited under — the **real client IP** for an anonymous request (behind a
  global cardinality cap), the JWT **`sub`** for an authenticated one — and reject excess with `429` +
  `Retry-After`.

- **Collaborators:**
  - `CleansiaStartupBase` — registers the policies (`AddPolicy`), the `ForwardedHeadersOptions` + the
    startup guard, the global anonymous cardinality cap, and the pipeline order (`UseForwardedHeaders`
    at the top → `UseAuthentication` → `UseRateLimiter`, with CSRF `UseHostAuthMiddleware` unchanged
    after the limiter).
  - `ForwardedHeadersOptions` / the App Service ingress — supplies the real client IP (trusted
    `X-Forwarded-For`, **config-driven** `ForwardLimit`, **narrow** `KnownNetworks`; over-broad/unset →
    the app refuses to boot in non-dev).
  - `HttpContext.User` — supplies the validated `sub` for authenticated callers.
  - The global anonymous ceiling (D7) — bounds live-partition cardinality so a botnet of distinct real
    IPs cannot OOM the host.
  - The rejection metric / `GetStatistics` (D8) — exposes the control's own health (rejection counter by
    policy name, partition-count gauge, degraded-mode signal) — never the partition key (S6).
  - The `[EnableRateLimiting("auth"/"interactive")]` attributes — the consumers that opt endpoints in.

- **Does NOT know:**
  - Controller / endpoint identities or business rules, or *which* endpoints are tagged (the attribute's job).
  - The **request body** (attempted username, command fields) — the limiter runs before model binding;
    keying on body content is forbidden (ADR-0003 D2). If a scenario needs an attempted-account key, a
    *bound* collaborator must be added first.
  - The Stripe webhook source-IP ranges — that is SEC-W3's separate `"webhook"` policy.
  - Persisted auth-attempt / account-lockout state — a separate hardening story.
  - Per-host audience/role logic — partitioning is host-independent, decided per request in one shared callback.

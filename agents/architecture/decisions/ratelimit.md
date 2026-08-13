# Rate limiting — partitioned, bounded, shared across hosts

**Status:** Accepted (ADR-0003 / ADR-RATELIMIT, 2026-06-01). Canonical decision:
`docs/decisions/adr-0003.md`. This page is the operator/developer summary;
the ADR governs on any conflict.

## The decision in one paragraph
The shared rate limiter in `CleansiaStartupBase` is **partitioned** — anonymous requests are limited
**per real client IP**, authenticated requests **per JWT `sub`** — instead of one global bucket per
host. It is **cardinality-bounded** (a botnet of distinct real IPs cannot OOM the box) and **defined
once** for all five hosts. Policy names (`"auth"`, `"interactive"`) and the `[EnableRateLimiting]`
attributes are unchanged, so no controller, DTO, NSwag, or migration work.

## Why
The old limiter (`CleansiaStartupBase.cs:69-93`) used `AddFixedWindowLimiter` with no partition key —
**one global bucket shared by everyone**. One client could spend the 10/min `auth` budget and lock out
all logins on a host (BSP-4 major / IDA-SEC-02 critical). Behind the App Service front end, client IP
is also not visible without `UseForwardedHeaders` (absent from `src/`), so partition key and
forwarded-headers trust are one decision.

## What changed (developer-facing)
| Area | Before | After |
|---|---|---|
| `auth` partition | global bucket | `auth:ip:{ip}` (anon) · `auth:sub:{sub}` (authenticated) |
| `interactive` partition | global bucket | `interactive:ip:{ip}` · `interactive:sub:{sub}` |
| `auth` limit | 10/min global | 10/min per anon IP · **30/min per `sub`** (real checkout fits) |
| `interactive` limit | 60/min global | 60/min per partition |
| Forwarded headers | none | `UseForwardedHeaders` at top of pipeline (config-driven, guarded) |
| Pipeline order | `UseRateLimiter` **before** `UseAuthentication` (bug) | after `UseAuthentication`; CSRF unchanged after the limiter |
| Cardinality | unbounded | anonymous IP partitions behind a global ceiling |
| Limit hit | `429` | `429` + `Retry-After` (lease metadata, else window+jitter) |
| Observability | none | rejection counter (by policy name), partition-count gauge, degraded-mode signal → App Insights |

Honest single callers see **no behavior change** (their own per-partition allowance is strictly more
permissive than the old shared pool). An adversary holding one valid account is bounded to ≤2× on
mixed-auth controllers — accepted, and bounded further by the `/Register` throttle.

## Rules this locks in
- **Never hand-roll an un-partitioned `AddFixedWindowLimiter`** — it's an S5 violation
  (`security-rules.md §S5`). Reuse the shared `AddPolicy` shape for any new per-user side-effect window.
- **Never ship an unbounded per-IP partition** — front it with a cardinality cap (D7).
- **Partitioning ≠ coverage** — a money/side-effect endpoint with no `[EnableRateLimiting]` is still an
  S5 gap (`BSP-4d`).
- **Don't log the partition key above `Debug`** (it embeds IP/`sub`) — S6. Metrics by policy name only.
- **Pipeline order is load-bearing:** `UseForwardedHeaders` top → `UseAuthentication` → `UseRateLimiter`
  → CSRF (`UseHostAuthMiddleware`) → `UseAuthorization`.

## Deploy / ops obligations (blocking)
- Set `ForwardedHeaders:KnownNetworks`/`KnownProxies`/`ForwardLimit` per environment to a **narrow**
  ingress network. Unset or over-broad (`/0`–`/8`) → **the app refuses to start** in non-dev. Confirm
  the live proxy chain first (**Q-RATELIMIT-02**, blocking).
- **Pin API instance count to 1.** The in-process limiter is per-replica; N replicas ≈ N× the limit.
  Scale-out is forbidden until a distributed (Redis/gateway) limiter ships (**Q-RATELIMIT-01**).
- Wire the D8 metrics to the existing App Insights alerts (`infrastructure.md:270-279`).

## Known residual risk (NOT fixed by this decision)
Closes the **single-source** DoS/lockout class. Does **not** close:
- **Distributed credential-stuffing** and **distributed confirmation-code guessing** — `ConfirmUserEmail`
  looks up by a **6-digit code alone** (10^6 space, `[AllowAnonymous]`); per-IP can't stop a botnet. Fix
  = `BSP-4b` (account-lockout / per-code throttle). **Owner decision on whether Wave 0 may ship this
  surface open: Q-RATELIMIT-03 (blocking).**
- **Account-farming** for a 2nd `auth` bucket (≤2×) — bounded by `/Register`, addressed by `BSP-4b`.
- **Cross-instance correctness if ever scaled out** — in-process limiter is per-replica (Q-RATELIMIT-01).

## Companion work (tracked, so each gap is a decision)
- `BSP-4b` (blocking companion) — account-lockout / per-confirmation-code throttle + `/Register` bound.
- `BSP-4c` — SPA/mobile client-side `Retry-After` back-off jitter.
- `BSP-4d` — `[EnableRateLimiting]` coverage for uncovered money endpoints (Membership/Dispute/
  RecurringBooking/Device + Partner payroll controllers).
- **SEC-W3** — a separate `"webhook"` per-IP policy for the Stripe webhook; depends on this decision for
  `UseForwardedHeaders` + `ClientIp`, but is independent (a 429 to Stripe is a retry trigger).

## Verify it's working (load-bearing checks from the ADR)
- #2 fails on current code, passes after the fix (client A's 429s don't touch client B);
- #4 the over-broad/empty `KnownNetworks` boot refusal;
- #5 the complete pipeline order incl. CSRF position;
- #8 the cardinality bound;
- #9 a realistic authenticated checkout never hits a 429.

---
id: T-0115
title: Partitioned rate limiter + UseForwardedHeaders + startup guard + cardinality cap
status: done
size: M
owner: —
created: 2026-06-01
updated: 2026-06-15
depends_on: []
blocks: []
stories: [US-partner-0042]
adrs: [0003]
layers: [config, backend]
security_touching: true
manual_steps: []
sprint: 0
source: ADR-0003; findings BSP-4/IDA-SEC-02
pairs_with: T-0126
---

## Context

The rate limiter is registered in `src/Cleansia.Config/Abstractions/CleansiaStartupBase.cs:69-93`
as **two un-partitioned named fixed-window limiters** (`AddFixedWindowLimiter("auth", …)` at :76-81,
`AddFixedWindowLimiter("interactive", …)` at :87-92). A named limiter with no partition key is **one
global bucket shared by every caller** on that host. This is two inseparable defects on the same
single decision (how the limiter partitions callers):

- **IDA-SEC-02 (critical) / BSP-4 (major) — no per-caller isolation.** One client can spend all 10
  `auth` permits/min on `/Login` and lock out every legitimate login/register/reset/refresh/
  confirm-email request on that host for the rest of the window — a trivial single-source DoS, and
  brute-force is under-throttled. (Audit: `findings.md:49`, INDEX `BSP-4 / IDA-SEC-02` Wave 0.)
- **The proxy collapses any naive per-IP fix.** The codebase reads the client IP raw via
  `context.Connection.RemoteIpAddress` and there is **no `UseForwardedHeaders` registered anywhere**
  (verified: zero matches in `src/`). Behind the App Service front end every request's
  `RemoteIpAddress` is the front-end IP, so a per-IP partition keyed on `RemoteIpAddress` alone
  re-creates the global bucket. Partition key + forwarded-headers trust + cardinality bound are one
  decision — frozen by **ADR-0003 (ADR-RATELIMIT, accepted 2026-06-01)**.

This ticket implements ADR-0003 deliverable 1 (the in-app code change). It does **not** fold in
SEC-W3 (webhook policy) or BSP-4d (attribute coverage). Built **TEST-FIRST** per
`agents/knowledge/testing.md`; its test pair is **T-0126** (limiter partition-isolation harness +
cases), which lands in the same merge.

## Acceptance criteria

- [ ] **AC1 — Per-IP isolation (anonymous).** Given the host booted through the real pipeline with
  forwarded-headers configured for the test network, When client A (IP `10.0.0.1`) sends 11 anonymous
  `POST /Auth/Login` in one window and client B (IP `10.0.0.2`) sends its first, Then A's 11th is
  `429` and B's first is **not** `429`. (ADR-0003 §verify #2; US-partner-0042 AC-1/2/6.)
- [ ] **AC2 — Per-`sub` isolation (authenticated).** Given two authenticated users on an
  `auth`-tagged authenticated endpoint, When user X exhausts X's window from the **same IP** as user
  Y, Then only X is `429` and Y is unaffected. (verify #3.)
- [ ] **AC3 — No un-partitioned limiter remains.** Given the codebase, Then `AddFixedWindowLimiter(`
  does not appear in any `*Startup*.cs` (or anywhere); `"auth"`/`"interactive"` are registered via
  `options.AddPolicy(name, …)` using `RateLimitPartition.GetFixedWindowLimiter`. (verify #1.)
- [ ] **AC4 — Forwarded-headers trust + spoof rejection.** Given a **narrow** `KnownNetworks`, When
  distinct trusted-proxy `X-Forwarded-For` client IPs arrive Then they land in distinct partitions;
  When a spoofed XFF arrives from an **untrusted** hop Then it is ignored (keyed by connection IP).
  (verify #4.)
- [ ] **AC5 — Startup guard fails closed (non-dev).** Given a **non-Development** environment, When
  `KnownNetworks` AND `KnownProxies` are both empty/unset, OR a `KnownNetworks` prefix is `/0`–`/8`,
  OR it contains `0.0.0.0/0` / `::/0`, Then the host **refuses to boot** (assert it throws). In
  Development an empty config boots and degrades to one bucket. (D3; verify #4.)
- [ ] **AC6 — Pipeline order (complete band, incl. CSRF).** Given `CleansiaStartupBase.Configure`,
  Then `UseForwardedHeaders` precedes `RequestLogging` (currently `:120`) and `UseRateLimiter`;
  `UseRateLimiter` runs **after** `UseAuthentication`; and `UseHostAuthMiddleware` (CSRF) stays
  between the limiter/auth band and `UseAuthorization` (its position at `:137` is unchanged).
  (D4; verify #5.)
- [ ] **AC7 — Single definition, no per-host override.** Given the five host projects (`Web.Admin`,
  `Web.Partner`, `Web.Customer`, `Web.Mobile.Partner`, `Web.Mobile.Customer`), Then **zero** calls
  to `AddRateLimiter` / `AddFixedWindowLimiter` / `AddPolicy("auth"/"interactive")` exist outside
  `CleansiaStartupBase`. (D5; verify #6.)
- [ ] **AC8 — Per-partition limits + behavior preserved.** Then `auth` IP partition = 10/min, `auth`
  `sub` partition = 30/min, `interactive` = 60/min; `Window = 1 min`, `QueueLimit = 0`,
  `RejectionStatusCode == 429` (`:71` unchanged); all four limits config-overridable. (D6; verify #7.)
- [ ] **AC9 — Cardinality bound.** Given a spray of many distinct synthetic client IPs (via trusted
  XFF), Then live partitions do **not** grow without bound — the global anonymous ceiling rejects
  with `429` past the cap (`RateLimiting:Anon:GlobalCeiling`). (D7; verify #8.)
- [ ] **AC10 — `Retry-After` on rejection.** Given a rejected request, Then the `429` response
  carries a `Retry-After` header (lease metadata when present, else window + 0–15s jitter). (D6.)
- [ ] **AC11 — Honest-session false-positive guard.** Given a scripted realistic authenticated
  checkout (create-order + 3 declined-card retries + re-quote + confirm ≈ 8 mutations in <60s), Then
  it completes with **no** `429` (proves the per-`sub` `auth` limit of 30). (D6; verify #9.)
- [ ] **AC12 — Observability (S6-safe).** Then a rejection emits a counter dimensioned by **policy
  name only** (never the partition key), a partition-count gauge is exported, and a degraded-mode
  signal fires when forwarded-headers are unconfigured. The partition key is never logged above
  `Debug`. (D8; verify #10.)

## Out of scope

- **SEC-W3** — the separate `"webhook"` per-source-IP policy (this ticket only guarantees the shape
  supports it: `AddPolicy` allows N policies, `ClientIp(ctx)` + the D3 fix are reusable). SEC-W3
  depends on this landing first.
- **BSP-4d** — adding `[EnableRateLimiting]` to currently-uncovered money/side-effect endpoints. No
  controller attribute edits here (policy names are preserved on purpose).
- **BSP-4b** — account-lockout / per-confirmation-code throttle (distributed-guessing residual,
  Q-RATELIMIT-03). **BSP-4c** — client-side `Retry-After` back-off jitter.
- No EF migration, no NSwag regen, no DTO change (policy names unchanged → existing
  `[EnableRateLimiting]` sites untouched).
- Infra deploy gate (setting `ForwardedHeaders:*` per env, pinning instance count to 1) is the
  owner's manual deploy checklist, not this code ticket — but the D3 guard makes a wrong/missing
  value a boot failure, so it is enforced, not merely documented.

## Implementation notes

**Serialization cluster (TICKET-MAP):** `CleansiaStartupBase.cs` (pipeline + limiter) cluster —
strict order **BSP-4 → SEC-W3 → PROD-CONFIG(BSP-5 hop)**. This ticket is the **head** of that
cluster; SEC-W3 and PROD-CONFIG must NOT run concurrently with it (all edit the startup pipeline /
limiter). It must land before its cluster successors.

**Governing ADR: ADR-0003** (`agents/backlog/adr/0003-partitioned-rate-limiting.md`) — read it in
full; it is immutable and dictates every decision below. Key edit sites in
`src/Cleansia.Config/Abstractions/CleansiaStartupBase.cs`:
- Replace `AddFixedWindowLimiter` at `:76-92` with `options.AddPolicy("auth", …)` /
  `options.AddPolicy("interactive", …)` + `options.OnRejected = …` (ADR D1).
- Add `services.Configure<ForwardedHeadersOptions>(…)` + `AddOptions<…>().Validate(guard)` in
  `ConfigureServices` (ADR D3 parts 2 & 3).
- In `Configure`, insert `app.UseForwardedHeaders()` immediately after the `EnableBuffering`
  `Use(...)` block (`:104-108`) and **before** `app.UseMiddleware(RequestLoggingMiddlewareType)`
  (`:120`); move `app.UseRateLimiter()` (`:132`) to run **after** `app.UseAuthentication()`
  (`:133`); leave `UseHostAuthMiddleware(app)` (`:137`) where it is (ADR D4).
- Partition-key functions (`AuthPartition` / `InteractivePartition` / `ClientIp`), the global
  anonymous cardinality cap, and `OnRejected` + metrics live in the shared base too — see ADR D2,
  D6, D7, D8 for the exact shapes. New role: `agents/knowledge/roles/rate-limit-policy.md`.

**TEST-FIRST (knowledge/testing.md).** This is security + pure-pipeline logic → strict. The test
pair **T-0126** is written first and lands in the same merge: it requires a **new
`WebApplicationFactory`-style host harness** — the existing `Cleansia.IntegrationTests` harness
(`BaseIntegrationTest.cs` / `PostgresContainerFixture.cs`) is MediatR/DbContext-level and **cannot**
boot a host with `ForwardedHeaders` config to exercise the limiter middleware (ADR Rollout #1).
Building that harness is part of this deliverable. The status log must show red→green and each AC
mapped to a case. Reviewer enforces Gate 6 against the ADR's "How a reviewer verifies" list (#1–#10).

**Security gate mandatory** (`security_touching: true`): closes IDA-SEC-02 (critical). Reviewer +
security verdicts required before `done`. Residual risks (distributed stuffing, confirmation-code
guessing) are accepted for v1 and tracked by BSP-4b — do not re-litigate here.

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-03 — in_progress (backend). Implementing ADR-0003 deliverable 1 TEST-FIRST.
- 2026-06-03 — RED. Wrote the failing tests first (all under `src/Cleansia.Tests/RateLimiting/`):
  source-scan AC3/AC7 (`RateLimiterSourceContractTests`), partition fns AC2 logic
  (`RateLimitPartitionTests`), guard AC5 (`ForwardedHeadersGuardTests` + end-to-end
  `ForwardedHeadersBootTests`), pipeline AC6 (`PipelineOrderTests`), behavioral AC1/AC2/AC4/AC8/AC9/
  AC10/AC11 (`RateLimiterHostBehaviorTests` via the new TestServer harness), observability AC12
  (`RateLimitObservabilityTests`). Confirmed RED for the right reason: `Cleansia.Config.RateLimiting`
  / `RateLimitPolicies` / `RateLimitMetrics` did not exist (CS0234), and the source-scan/pipeline
  tests failed against the un-partitioned base.
- 2026-06-03 — GREEN. Implemented D1–D8 in new `src/Cleansia.Config/RateLimiting/RateLimitPolicies.cs`
  + `RateLimitMetrics.cs`, wired into `CleansiaStartupBase` (D1 register, D3 ForwardedHeaders +
  fail-closed guard, D4 pipeline reorder: `UseForwardedHeaders` at top, `UseRateLimiter` after
  `UseAuthentication`, CSRF unchanged). Removed both `AddFixedWindowLimiter` registrations. Built the
  reusable TestServer host harness (`RateLimiting/Harness/RateLimiterHostHarness.cs`) that boots the
  REAL limiter middleware with synthetic XFF — usable by T-0126.
  - **Build:** `dotnet build Cleansia.Api.sln -c Debug` → Build succeeded, 0 errors.
  - **Tests:** `dotnet test src/Cleansia.Tests` → 278 passed / 0 failed (47 new rate-limiting tests,
    all green; each AC maps to ≥1 case). Harness reality: the host-level ACs (AC1/AC2/AC4/AC9/AC11)
    run IN-PROCESS via `Microsoft.AspNetCore.TestHost` (no Docker, no Postgres) — proven here, NOT
    deferred. The existing `Cleansia.IntegrationTests` (Postgres/MediatR) harness cannot boot a host
    pipeline, hence the new harness (ADR Rollout #1).
  - **Manual / deploy gate (unchanged from ADR):** set `ForwardedHeaders:KnownNetworks`/
    `KnownProxies`/`ForwardLimit` per env to a narrow ingress network; pin instances to 1. The D3
    guard makes a wrong/missing value a boot failure. No EF migration, no NSwag regen, no controller
    edits (policy names preserved). Not committed/pushed.

## Review
**Reviewer — APPROVED (2026-06-03).** Walked the ADR-0003 "how a reviewer verifies #1–#10" gate against the
real code, re-ran build + tests, grepped source independently. All 12 ACs satisfied: no `AddFixedWindowLimiter`
anywhere (both policies via `AddPolicy` + `RateLimitPartition.GetFixedWindowLimiter`); per-IP (anon, 10/min) /
per-`sub` (authed, 30/min) / interactive (60/min) partitions; `UseForwardedHeaders` at top + narrow
`KnownIPNetworks` + spoofed-XFF-from-untrusted ignored; fail-closed startup guard (non-dev throws on
empty/over-broad config incl. /0–/8 + `0.0.0.0/0`/`::/0`; dev degrades); pipeline order correct
(ForwardedHeaders → … → UseAuthentication → UseRateLimiter → CSRF unchanged → UseAuthorization); single
definition, zero host overrides; Retry-After; cardinality cap; S6-safe metrics (policy name only). Used the
real .NET 10 `KnownIPNetworks` API (ADR's `KnownNetworks` sketch was the obsolete name). Flagged (not a
defect): working-tree migration churn is the owner's Initial regen, unrelated to this ticket (touches no entities).

**Security — PASS (2026-06-03).** Closes critical IDA-SEC-02: the single-source DoS is gone — callers are
isolated per-IP/per-sub so one flood can't 429 everyone; the per-IP key uses the TRUSTED forwarded IP (spoofed
XFF from an untrusted hop ignored — partition not evadable); a misconfigured prod REFUSES TO BOOT (no silent
degrade to one bucket); the cardinality cap prevents partition-explosion DoS; S6 — partition key never logged
above Debug, metrics by policy name only. Accepted residuals (distributed stuffing, confirmation-code guessing)
correctly tracked to BSP-4b, not re-litigated.

**Verification (orchestrator, independent):** `AddFixedWindowLimiter` = 0 hits in production src; pipeline order
read by line (`UseForwardedHeaders`:101 → `UseAuthentication`:127 → `UseRateLimiter`:128 → CSRF:133 →
`UseAuthorization`:134 — correct; my initial regex flagged a false negative off a comment, confirmed clean on
read). `dotnet build Cleansia.Api.sln` = 0 errors; `dotnet test Cleansia.Tests` = **278 passed / 0 failed**
(was 231; +47 rate-limit tests). **All 12 ACs proven in-process via a new `Microsoft.AspNetCore.TestHost` host
harness — nothing deferred** (the harness is reusable for T-0126 + future host-level authz tests). New
`TestHost` dep is test-only. No EF migration, no nswag, no controller edits (policy names preserved). Not committed.
⚠️ Owner deploy gate (per ADR, enforced by the boot guard): set `ForwardedHeaders:*` per env to a narrow ingress
network + pin instances to 1.

- 2026-06-03 — done (reviewer APPROVED vs ADR verify #1–#10 + security PASS; build 0 errors, 278 tests, full
  host harness, nothing deferred; independently re-verified by orchestrator). **Head of the CleansiaStartupBase
  cluster is now landed — SEC-W3 (T-0116) + PROD-CONFIG may proceed.** NOT committed.

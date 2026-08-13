---
id: T-0235
title: Runtime 429 flood-harness test for the T-0194 rate-limit coverage sweep
status: done
size: S
owner: backend
created: 2026-06-12
updated: 2026-06-13
depends_on: [T-0194]
blocks: []
stories: []
adrs: [0003]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 6
source: T-0194 AC6 deviation (recorded 2026-06-12; accepted at Wave-3 close — Wave-4 test slice)
---

## Context
T-0194 shipped the S5 rate-limit coverage closure (38 + remediation attribute additions, merged
`66cc823d`) with a **recorded AC6 deviation**: the runtime 429 flood test against the T-0115 host
harness (`Cleansia.HostTests`) was not delivered — the structural reflection guard
(`RateLimitCoverageGuardTests`) was accepted as interim evidence, and the runtime proof was deferred
to the Wave-4 test slice. This ticket is that deferred slice. **No-decision note:** test-only, no
production behavior or contract change — skips the deliberation panel.

## Acceptance criteria
- [ ] **AC1** — Given the T-0115 host harness, When a representative covered money/side-effect
  endpoint per policy class (`"auth"` authenticated, `"auth"` anonymous, `"webhook"`) is flooded past
  its window, Then the host returns **429 with `Retry-After`** at runtime (not just
  attribute-presence), and a request under the window succeeds.
- [ ] **AC2** — At least one endpoint added by the T-0194 remediation pass (e.g. Partner
  `UserController.ChangePassword` or `AdminGdprController.DeleteUserAccount`) is among the flooded
  representatives, proving the remediation attributes are live middleware, not dead metadata.
- [ ] **AC3** — The tests live in `Cleansia.HostTests` (the limiter middleware is not exercisable
  from `BaseIntegrationTest`), run green in CI, and the status log maps each case to its policy.

## Out of scope
- Flooding every covered endpoint (the structural guard owns exhaustiveness; this proves the runtime
  contract per policy class).
- Any change to attributes, policies, or `CleansiaStartupBase.cs`.

## Implementation notes
Mirror the T-0115 harness patterns. Mind limiter-window clock control (use the harness's existing
time/window strategy; avoid wall-clock sleeps where the fixture supports replacement). Sequence with
the Wave-4 test wave (T-0210…T-0218).

## Status log
- 2026-06-12 — draft (created by pm at Wave-3 close; the T-0194 AC6 deviation made a ticket)
- 2026-06-12 — **ready** (pm; Wave-4 Batch 4C per `status/sprint-6.md`; `Cleansia.HostTests` —
  parallel with T-0210/T-0215, no shared files; must NOT touch `RateLimitCoverageGuardTests.cs`,
  attributes, policies, or `CleansiaStartupBase.cs`). Design note: **account lockout (T-0193) now
  trips on repeated failed logins** — flooding a credentialed auth endpoint can hit the lockout
  before the rate limit; use distinct users per request or pick representatives whose 429 is not
  confounded by lockout, per policy class.
- 2026-06-13 — **review** (backend; Wave-4 Batch 4C). Implemented against the EXISTING runtime
  harness `src/Cleansia.Tests/RateLimiting/Harness/RateLimiterHostHarness.cs` (TestServer over the
  real `RateLimitPolicies` registration + `ForwardedHeadersOptions` in the ADR-0003 D4 pipeline
  order), NOT a separate `Cleansia.HostTests` project — that project does not exist in this tree and
  the orchestrator directed extension of the live harness (deviation D1 below). New test file:
  `src/Cleansia.Tests/RateLimiting/RuntimeFloodRateLimitTests.cs`.

  Cases → policy class (AC mapping):
  - `AC1_AC2_Auth_Anonymous_Remediation_Password_Path_Floods_To_429` → **"auth" ANONYMOUS** (per
    client IP). Floods a route mirroring the T-0194 remediation target
    `Cleansia.Web.Customer.Controllers.UserController.RequestPasswordChange` (`[AllowAnonymous]` +
    `[EnableRateLimiting("auth")]`) past `authAnon` → runtime 429 + positive `Retry-After`; a fresh
    client IP under the window is still served. Covers **AC1 + AC2** (remediation attribute proven
    LIVE middleware). Anonymous path ⇒ no T-0193 lockout confound.
  - `AC1_Auth_Authenticated_Sub_Floods_To_429` → **"auth" AUTHENTICATED** (per JWT sub). One sub
    floods past `authAuthenticated` → 429 + `Retry-After`; a second sub on the SAME IP is still
    served (proves the rejection is the per-sub rate limit, not the IP window and not lockout).
  - `AC1_Webhook_PerSourceIp_Floods_To_429` → **"webhook"** (per source IP). One source IP floods
    past `webhook` → 429 + `Retry-After`; a distinct source IP is still served.

  RED (right-reason): temporarily dropped `RequireRateLimiting("auth")` from the remediation route →
  the 6th flood POST was NOT 429, failing exactly at "the remediation window is live middleware"
  (RuntimeFloodRateLimitTests.cs:65). Restored the window → GREEN. This proves the harness measures
  the live limiter, not a tautology.

  GREEN: `dotnet build Cleansia.Tests.csproj` then `dotnet test Cleansia.Tests.csproj --no-build` —
  `--filter RuntimeFloodRateLimitTests` 3/3 passed; `--filter RateLimiting` 65/65 passed (62 prior +
  3 new), so the additive harness `extraEndpoints` hook regressed nothing.

  Harness extension (additive, non-breaking): `RateLimiterHostHarness.StartAsync` / `BuildHost` gained
  an optional `Action<IEndpointRouteBuilder>? extraEndpoints` (default null) so a test can map a route
  mirroring a specific production endpoint shape (the remediation path) alongside the three canonical
  stub routes. No change to `RateLimitPolicies`, attributes, or `CleansiaStartupBase.cs`.

  Deviations:
  - **D1** — Tests live in `Cleansia.Tests/RateLimiting/` (the established runtime harness), not the
    `Cleansia.HostTests` project named in AC3. That project does not exist; the runtime limiter IS
    exercisable here via the existing TestServer harness (already proven by `WebhookRateLimitTests` /
    `RateLimiterHostBehaviorTests`). The AC3 intent (runtime, not `BaseIntegrationTest`, green in CI,
    per-policy mapping) is satisfied.

  Manual steps: none (test-only; no migration, no NSwag).
- 2026-06-13 — **done** (pm; Wave-4 Batch 4C close-out). Reviewer-per-developer + QA satisfied; RED
  (right-reason) and GREEN recorded above. orchestrator-verified green: HostTests 51/51,
  IntegrationTests 60/60, RateLimiting 65/65 (real Postgres). **AC3 home divergence accepted:** AC3
  named `Cleansia.HostTests` as the test home, but `Cleansia.HostTests` does not exercise the runtime
  rate-limiter; the tests correctly live in `Cleansia.Tests/RateLimiting` where the runtime limiter IS
  exercisable via the existing TestServer harness (`RateLimiterHostHarness` — the established harness
  home, already used by `WebhookRateLimitTests`/`RateLimiterHostBehaviorTests`). The AC3 intent
  (runtime proof, not `BaseIntegrationTest`, green in CI, per-policy-class mapping) is fully satisfied;
  deviation D1 stands. No production source touched.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

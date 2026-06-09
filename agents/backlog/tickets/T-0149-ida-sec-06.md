---
id: T-0149
title: Refresh-token rotation re-checks profile (per host), not only audience
status: done
size: S
owner: backend
created: 2026-06-01
updated: 2026-06-07
depends_on: [T-0100]
blocks: []
stories: []
adrs: [0001]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 1
source: ADR-0001 D5; finding IDA-SEC-06
---

## Context

Finding **IDA-SEC-06** (severity: major, security/gap): the Customer and Partner host refresh
endpoints rotate a refresh token while re-checking only the token's **audience**, not the user's
current **profile**. A demoted user (or a user whose profile no longer matches the host) can keep
minting fresh access tokens at a host whose role they no longer hold, as long as the audience still
matches. Only the Admin host re-pins profile today
(`AdminAuthController.cs:48` passes `RequiredProfile = UserProfile.Administrator`).

The shared `RefreshToken` handler **already** supports the gate — it re-checks `user.IsActive`
(`RefreshToken.cs:69`) and, when `RequiredProfile` is supplied, rejects mismatches
(`RefreshToken.cs:75-79`); the new access token's role claims are re-read from the current DB
`user.Profile` via `user.SetClaims` (`RefreshToken.cs:109`). The hole is purely at the **call sites**:
the four non-admin controllers pass `RequiredAudience` but omit `RequiredProfile`.

**ADR-0001 D5 §3** governs and freezes the contract: *every host's refresh endpoint MUST pass BOTH
`RequiredAudience` (its own) AND `RequiredProfile` (the profile that host serves)*, with the per-host
mapping below. D5 §3 also reconciles this with D5 §2: the re-check binds a refresh token to a
**profile**, not to a host within the shared Customer trust zone (a Web.Customer refresh token
remains, by design, redeemable at Mobile.Customer — same audience, same profile). This **adapts S1**
(server-truth identity): re-derive role/profile from the DB at every refresh, not only at login.

Source: `agents/backlog/adr/0001-authorization-model.md` D5 §3 (lines 512-534);
finding IDA-SEC-06 in `agents/backlog/audits/AUDIT-2026-06-01-findings.json`.

Per-host `RequiredProfile` to apply (ADR-0001 D5 §3):
- Web.Customer & Mobile.Customer → `UserProfile.Customer`
- Web.Partner & Mobile.Partner → `UserProfile.Employee`
- Web.Admin → `UserProfile.Administrator` (already correct — do not touch)

## Acceptance criteria

- [ ] **AC1** — Given a valid refresh token minted for a **Customer** whose DB profile has since
  changed to non-Customer (e.g. `Employee`/`Administrator`), When `POST /Auth/RefreshToken` is called
  on **Web.Customer** *or* **Mobile.Customer**, Then rotation fails with
  `BusinessErrorMessage.InvalidRefreshToken` and no new access token is issued (the controller now
  passes `RequiredProfile = UserProfile.Customer`).
- [ ] **AC2** — Given a valid refresh token whose user's DB profile no longer equals `Employee`,
  When `POST /Auth/RefreshToken` is called on **Web.Partner** *or* **Mobile.Partner**, Then rotation
  fails with `InvalidRefreshToken` (the controller now passes `RequiredProfile = UserProfile.Employee`).
- [ ] **AC3** — Given a valid refresh token whose user's DB profile **matches** the host's served
  profile (Customer at a customer host, Employee at a partner host), When `RefreshToken` is called,
  Then rotation **succeeds** and the new access token carries role claims re-read from the current DB
  `user.Profile` — i.e. the legitimate path is unbroken on all four hosts.
- [ ] **AC4** — Given a **Web.Customer** refresh token presented to **Mobile.Customer** (same
  `cleansia.customer` audience, same `Customer` profile), When `RefreshToken` is called, Then rotation
  **succeeds** — the profile gate must NOT add host-binding within the one Customer trust zone
  (ADR-0001 D5 §2/§3). This pins the intended non-behavior so a future audience split is a conscious,
  test-breaking decision.
- [ ] **AC5** — Web.Admin (`AdminAuthController.cs:48`) is unchanged and still passes
  `RequiredProfile = UserProfile.Administrator`; the existing audience pin on every host is retained.
- [ ] **AC6** — Tests (written **test-first**, per `knowledge/testing.md`) prove the gate: a
  handler-level unit test asserting `RequiredProfile` mismatch → `InvalidRefreshToken` and match →
  success, plus per-host controller/integration coverage for the four hosts capturing the
  mismatch-rejects (AC1/AC2), match-succeeds (AC3), and the intra-Customer-zone cross-host success
  (AC4). Each AC maps to a named test case; the test predates the call-site change in the diff/commits.

## Out of scope

- Renaming the misleading `JwtAudiences.Mobile` constant (D5 §2 — non-behavioral follow-up; touches
  issued-token compatibility; not a drive-by here).
- Splitting the Customer pair onto a `cleansia.mobile.customer` audience (D5 Alternatives / Q-0003 —
  would require a superseding ADR).
- `IDA-SEC-07` (`HasAdminAccess` default) — separate finding/ticket; do not change that DTO here.
- Any change to `RefreshToken.cs` handler logic — the `RequiredProfile` gate already exists; this
  ticket only wires the call sites (plus its missing tests).

## Implementation notes

**TEST-FIRST** per `agents/knowledge/testing.md` (refresh rotation is an auth/lifecycle gate, not
pixels): write the red handler unit test + the per-host integration tests stating the rejected/
accepted branches **before** touching the controllers; status log must note "red → green". Auth gate
changes go to the **Security gate** (`security_touching: true`).

Governed by **ADR-0001 D5 §3** (per-host `RequiredProfile` mapping is the frozen contract). Do not
re-litigate the Customer one-trust-zone decision (D5 §2) — AC4 pins it as intended.

Call sites to change (add `RequiredProfile`, keep existing `RequiredAudience`):
- `src/Cleansia.Web.Customer/Controllers/AuthController.cs:90` — `RequiredProfile = UserProfile.Customer`
- `src/Cleansia.Web.Mobile.Customer/Controllers/AuthController.cs:101` — `RequiredProfile = UserProfile.Customer`
- `src/Cleansia.Web.Partner/Controllers/AuthController.cs:101` — `RequiredProfile = UserProfile.Employee`
- `src/Cleansia.Web.Mobile.Partner/Controllers/AuthController.cs:106` — `RequiredProfile = UserProfile.Employee`

Already correct, leave as-is: `src/Cleansia.Web.Admin/Controllers/AdminAuthController.cs:48`.

Handler reference (no change): `src/Cleansia.Core.AppServices/Features/Auth/RefreshToken.cs` —
`RequiredProfile` gate at `:75-79`, active-check `:69`, claims re-read `:109`. `UserProfile` values:
`Customer = 1`, `Employee = 2`, `Administrator = 100`
(`src/Cleansia.Core.Domain/Enums/UserProfile.cs`).

**Serialization cluster:** none. The four AuthControllers are distinct files and none appears in the
TICKET-MAP shared-file clusters (`PolicyBuilder`, `CleansiaStartupBase`, `UnitOfWorkPipelineBehavior`,
host `ServiceExtensions.cs`, dispute handlers, `CreateOrder.cs`, `LoyaltyService.cs`) — this ticket
can run in parallel with non-Auth backend work. Dependency: T-0100 (BSP-1) for the post-Wave-0
authorization/host baseline.

No `manual_steps` — the `RefreshToken.Command` DTO already exposes `RequiredProfile`, so no NSwag
regen and no migration are needed.

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-06 — ready (Batch 1B; dep T-0100 done ✓; four distinct AuthControllers, no serialization cluster
  collision → fully parallel. Routed to backend; **security gate mandatory** (security_touching — refresh
  rotation re-checks profile); reviewer + security in parallel).
- 2026-06-06 — backend in_progress (test-first). RED: wrote per-host controller-enrichment unit tests
  (`Cleansia.Tests/Controllers/RefreshTokenControllerProfileEnrichmentTests.cs`) + handler-gate unit tests
  (`Cleansia.Tests/Features/Auth/RefreshTokenProfileGateHandlerTests.cs`) + per-host end-to-end integration
  tests (`Cleansia.IntegrationTests/Features/Auth/RefreshTokenProfileGateTests.cs`) BEFORE the call-site
  change. Confirmed red: the 4 non-admin enrichment tests failed (RequiredProfile = null), Admin passed,
  handler-gate tests passed (gate already exists). GREEN: wired the 4 non-admin call sites to pass
  `RequiredProfile` (Customer hosts → UserProfile.Customer; Partner hosts → UserProfile.Employee), keeping
  the existing `RequiredAudience` (Mobile.Partner keeps `JwtAudiences.Mobile` — rename out of scope).
  Admin (`AdminAuthController.cs`) untouched. Handler (`RefreshToken.cs`) untouched per ticket. Build:
  `dotnet build src/Cleansia.Api.sln` 0 errors. Tests: 510/510 unit pass; 7/7 new integration pass
  (AC1–AC4 end-to-end). AC1–AC6 covered.
- 2026-06-07 — done (PM reconciliation: Wave-1 Batch 1B merged to master in a4f14094 / PR #73 chain; status corrected from ready/draft to done; reviewer+security gates were satisfied in the merged PR per sprint-3 closeout).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

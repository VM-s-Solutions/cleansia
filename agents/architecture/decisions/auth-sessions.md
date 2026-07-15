# Auth sessions — token lifetimes, device revocation, forced sign-out

**Status:** ADR-0024 accepted (panel verdict 2026-07-15; the 30-min mobile TTL is live — T-0405
landed). **ADR-0026 ACCEPTED (panel verdict 2026-07-15, amendments A1–A6; conditional only on the
owner ratifying that "immediately" tolerates ≤ 30 s — the question is in flight and does not gate
T-0414)** — partially supersedes ADR-0024 (D2
bound statement + D3-B deferral) on the owner's verbatim directive: *"I want the device once it's
deleted from the list of devices to be revoked IMMEDIATELY. Not wait until the token is revoked."*
**ADR-0027 ACCEPTED (panel verdict 2026-07-15, amendments U1–U3; not conditional on any new owner
question — it inherits ADR-0026's ≤ 30 s-vs-literal-zero question, does not re-ask it) — extends
ADR-0026 X1 to the user/password-reset dimension:** password reset ends the reset user's mobile
sessions within the same ≤ 30 s bound via a **sibling `RevokedUserDirectory`** keyed on `sub` and fed
from the persisted `password_reset` refresh-token rows (no migration); password CHANGE is deliberately
NOT accelerated. Canonical decisions:
`agents/backlog/adr/0024-mobile-access-token-ttl-is-the-device-revocation-latency-bound.md`,
`agents/backlog/adr/0026-immediate-device-revocation-via-device-id-claim-and-polled-revocation-directory.md`,
and `agents/backlog/adr/0027-immediate-user-session-cutoff-on-password-reset-via-polled-revoked-user-directory.md` (accepted).
This page is the evolving companion; the ADRs govern on any conflict. Prior related security notes:
`agents/backlog/security/auth-sessions.md` (tenant-filter symmetry on token reads, T-0236).

## The decision stack in one paragraph

Revoking a device kills its **refresh tokens instantly** (`RevokeDevice` → `RevokeByDeviceAsync`,
device-id carried across rotation — always worked, modulo the D9.7 rotation race, ticketed). ADR-0024
bounded the outstanding **access
JWT** at ≤ 30 min (mobile TTL 1440 → 30, config-only). ADR-0026 (accepted) closes the last gap —
the still-valid JWT itself: mobile-minted access tokens gain a **`device_id` claim**, and the two
mobile hosts check every validated token against an **in-memory `RevokedDeviceDirectory`** —
`(userId, deviceId, revokedAt)` for deactivations younger than the TTL, polled from Postgres every
≤ 30 s per instance. Token predates the revocation (`iat < revokedAt`) → `context.Fail` in
`OnTokenValidated` → **401** → the client's existing 401→refresh path hits the already-dead refresh
token → wipe + forced sign-out. Product bound: **a deleted device loses API access within ~30 s and
signs itself out at its next interaction.** The TTL stays as the fail-open backstop; push-driven
sign-out remains UX-only (a hostile client ignores a push — it is never the security bound).

## The shape under ADR-0026 (▲ = accepted 2026-07-15, implementation in T-0414)

| Piece | Value / mechanism | Where |
|---|---|---|
| Mobile access-token TTL | **30 min** — now the *backstop*, still ADR-governed (ClockSkew=Zero) | mobile hosts' `appsettings*.json` (pinned by TC-REVOKE-TTL-4) |
| ▲ Device-revocation bound (mobile) | **≤ `DeviceRevocation:RefreshSeconds` (default 30 s)** + in-flight completion, per instance, instance-count-independent | `RevokedDeviceDirectory` + refresher (`Cleansia.Config`, mobile hosts only) |
| ▲ Password-reset session-cutoff bound (mobile) | **≤ same ≤ 30 s poll** — reset user's pre-reset access tokens 401 (ADR-0027 accepted); keyed on `sub` (every token has it → **no claim-transition window**), fed from the persisted `password_reset` refresh-token rows (**no migration**); shares the `DeviceRevocation:Enabled`/`RefreshSeconds` bounds | sibling `RevokedUserDirectory` + refresher (`Cleansia.Config`, mobile hosts only) |
| ▲ Enforcement key | signed **`device_id` JWT claim** (login: `requestMetadata.DeviceId`; refresh: the **persisted** `issued.Record.DeviceId`, never the header) | `AuthExtensions.SetClaims` + both mint sites |
| ▲ Enforcement point | `JwtBearerEvents.OnTokenValidated` → `context.Fail("device_revoked")` → **401** (not 403 — the 401 drives the client machinery) | both mobile hosts' `AddJwt`, shared helper in Config |
| ▲ Snapshot source | Devices with `DeactivatedOn >= now − (TTL + 5 min)` — **no `IsActive` conjunct** (panel A1: `MarkRegistered` reactivation must not expunge a live revocation; the `iat` guard alone decides), `IgnoreQueryFilters` background read | new `IDeviceRepository.GetDeactivatedSinceAsync` |
| ▲ Failure posture | **fail-open on last snapshot** + staleness warning at 3× interval; ceiling = the TTL (fail-closed rejected: DB blip → fleet-wide forced sign-out via the refresh-failure conflation) | ADR-0026 D4 |
| ▲ Kill switch | `DeviceRevocation:Enabled` (ops relief valve), raw-file test-pinned `true` | four mobile appsettings |
| Web access-token TTL | 1440 min — **known finding, follow-up pending** (admin host first/separable) | web hosts' `appsettings*.json` |
| Refresh-token TTL | 30 d (rememberMe) / 1 d, sliding on rotation | `RefreshTokenService.Issue/RotateAsync` |
| Revocation write path | device row deactivated (▲ now stamps `DeactivatedOn` via `DeviceRepository.Deactivate` override) + refresh tokens revoked (`device_revoked`); rotation-reuse → chain revoke | `RevokeDevice.cs:43-44`, `RefreshTokenService.cs:57-71,120-133` |
| Mobile silent refresh | 401-reactive, single-flight, forced sign-out on refresh **rejection** | Android `AuthAuthenticator.kt`; iOS `SessionRefresher.swift` |
| Web silent refresh | 401-reactive interceptor + coordinator over HttpOnly cookies | `error.interceptor.ts` per app; `AuthCookieWriter.cs` |

## Rules this locks in (▲ = ADR-0026, accepted)

- **`AccessTokenExpMinutes` on a mobile host is a security bound** (ADR-0024, unchanged) — pinned
  by TC-REVOKE-TTL-4's raw-file test; superseding ADR required to change it.
- ▲ **`DeviceRevocation:Enabled` and `RefreshSeconds` are security bounds** — raw-file test-pinned
  (TC-REVOKE-NOW-7); superseding ADR required.
- ▲ **Enforcement keys on the signed claim, never a client-sent header** — the adversary *is* the
  client; `X-Device-Id` is bookkeeping at login/refresh only.
- ▲ **Device-deactivation write paths stamp `DeactivatedOn`** (`DeviceRepository.Deactivate`
  override, `SavedAddressRepository` precedent) — the directory's `RevokedAt` is a first-class
  audit timestamp.
- ▲ **The snapshot is reactivation-insensitive** (panel A1): the poll keys on `DeactivatedOn`
  alone — a re-registered (`MarkRegistered`) row must never expunge a live revocation; only the
  `iat` guard clears a session. Pinned by TC-REVOKE-NOW-9.
- ▲ **The refresher loop may not die** (panel A3): the whole tick sits in the loop's try/catch —
  an exception escaping `ExecuteAsync` would stop the host (`BackgroundServiceExceptionBehavior.
  StopHost`), and a dead loop cannot emit the staleness warning that is the only ops signal.
- ▲ **Any future bulk device-deactivation job must be checked against ADR-0026 first** — it
  inflates the snapshot and triggers a fleet-wide silent-refresh ripple (D9.6 standing rule).
- **Revocation latency claims:** ▲ "within ~30 seconds" on mobile (was "≤ mobile TTL"). Anyone
  promising literal-zero is describing the read-through escalation, which is not built.
- **Web sessions cannot be device-revoked** — `DeviceId = null`, deliberate null-guard
  (`RefreshTokenService.cs:129`). Unchanged; web tokens never carry the claim and never match.
- ▲ **(ADR-0027, accepted) Password RESET cuts off mobile sessions at ≤ 30 s; password CHANGE does
  NOT** — reset is unauthenticated takeover-recovery (keep-none), so `sub`-keyed cutoff is exact;
  change is authenticated hygiene that spares the caller's session, so accelerating it self-inflicts a
  401 on the trusted session. The user-directory feed keys on `RevokedReason == "password_reset"`
  alone. Web reset cutoff rides the standing web-host TTL follow-up, not ADR-0027.
- ▲ **(ADR-0027 D9.8) The `DeviceRevocation:Enabled` switch is SHARED across the device and user
  checks by decision** — accepted-risk coupling: it cannot express "device revocation off, reset
  cutoff on" (different blast radii). Split (`UserRevocation:Enabled`) is the pre-analyzed follow-up,
  triggered by an incident that needs exactly that asymmetry.
- ▲ **(ADR-0027 D9.9) Change-based recovery leaves ≤ 30-min attacker access** — a not-fully-locked-out
  victim who recovers via in-app `ChangeOwnPassword` (not the email RESET) is not accelerated;
  bounded by the TTL + the already-revoked sibling refresh chain. Closing it is a separate decision
  (spare-by-`iat`), not folded into ADR-0027.

## Trade-off space (kept live for the next revisit)

- **A (ADR-0024, shipped): 30-min TTL.** Config-only; now the backstop + horizon + claim-less bound.
- **B (ADR-0026, proposed — the bounded-staleness form): polled in-memory revocation directory.**
  Zero request-path DB cost (O(1) snapshot lookup), one tiny poll/instance/30 s, no new infra
  (Postgres is the backplane; no Redis exists). Staleness bound = poll interval, honest and pinned.
- **B-literal (named escalation, not built): read-through per-request `Device.IsActive` check.**
  +1 indexed query on every authenticated mobile request; swaps in behind `IRevokedDeviceDirectory`
  without touching hosts/claims/clients. **Trigger:** the owner rules "immediately" = 0 s.
- **B-fresher (named upgrade, not built): Postgres LISTEN/NOTIFY** as a second refresh trigger →
  ~0 s on the same design; adds a persistent-connection/reconciliation seam. **Trigger:** the 30 s
  bound is ever contested with evidence.
- **User-level session cutoff on password reset (ADR-0027, ACCEPTED — the X1 extension):** a *sibling*
  `RevokedUserDirectory` keyed on `userId`, fed from the persisted `password_reset` refresh-token rows
  (no schema change), consulted in the same `OnTokenValidated` helper (one extra O(1) probe).
  `iat < resetAt` → 401 within ≤ 30 s. Chosen over folding user entries into the device directory
  (breaks its composite-key contract + CRC boundary) and over a `security_stamp` column (needs a
  migration + a claim-transition window this design avoids by keying on `sub`). Password CHANGE
  deliberately excluded (D3); residues D9.8 (shared switch) / D9.9 (change-based recovery) named.
- **Split the shared kill switch → `UserRevocation:Enabled` (named, not built — ADR-0027 D9.8):** a
  distinct config key mirroring `DeviceRevocationOptions`, so an operator can disable device
  revocation without also disabling reset-recovery cutoff. **Trigger:** any incident that requires
  that asymmetry. Mechanical follow-up under a superseding/extending ADR.
- **User-disable (`IsActive = false`) cutoff (named extension, not built):** feed user disables into a
  user-level directory too. A further decision; today user-disable bites at ≤ 30 min via refresh
  re-check. ADR-0027's `RevokedUserDirectory` is the natural home if/when that is built (a second feed
  source), under its own superseding/extending ADR.
- **C: push-driven force-logout on `device_revoked`. UX complement ONLY — attacker-suppressible,
  never the security bound** (reaffirmed by ADR-0026 Alt (d)). Trigger unchanged: iOS receive-side
  (T-0403/T-0404); layers over enforcement, never substitutes.

## Watch items (post-rollout)

- **Spurious forced sign-outs (ADR-0024 D4.5, ticket filed):** *partially landed* — Android now
  classifies `RefreshResult.Unavailable` (keep session) vs `Rejected` (wipe)
  (`AuthAuthenticator.kt:94-105`); iOS still pending. ADR-0026's fail-open posture no longer leans
  on the conflation (panel A6) — it rests on bounded degradation (worst case = the TTL), which is
  permanent; fail-closed stays rejected either way.
- **Revoke↔rotation TOCTOU (ADR-0026 D9.7, hardening ticket X2):** a rotation racing the revoke's
  read→commit window can escape the chain revocation and then pass the directory (`iat` postdates
  the revoke). Pre-existing, milliseconds-wide, `auth`-bucket-capped; fix = `xmin` concurrency on
  `RefreshToken` and/or set-based conditional revocation.
- **Directory staleness telemetry:** the 3×-interval warning is the ops signal that the bound is
  degrading toward the TTL ceiling. First occurrence in production → check Postgres health, not
  the directory.
- **`auth` rate-limit bucket (ADR-0003):** unchanged from ADR-0024 — the 60 s fixed window is the
  binding constraint; watch the D8 counter. ADR-0026 adds no refresh traffic (the false-positive
  self-heal path costs one extra refresh per race occurrence — noise).
- **`Devices` poll query:** no index shipped; if it ever shows in pg_stat, a partial index
  (`WHERE "IsActive" = false`) is a one-line owner migration.
- **T-0406:** Android partner forced-signout *collector* still missing (tokens are wiped — the
  security property holds; the UI parks). More visible once revocation is ~immediate.
- **Change-based recovery residue (ADR-0027 D9.9):** a not-fully-locked-out victim who recovers via
  in-app `ChangeOwnPassword` (not the email RESET) leaves the attacker's access token alive ≤ 30 min
  — `password_changed` is deliberately not fed into the user directory (feeding it self-inflicts a 401
  on the caller's own spared session). Bounded by the TTL + dead refresh chain; closing it (spare-by-
  `iat`) is a separate decision, not scheduled.

## Open follow-ups

- **OWNER QUESTION (in flight, does not gate T-0414):** does "revoked IMMEDIATELY" tolerate the
  ≤ 30 s default bound, or must it be literal-zero → the B-literal read-through swap behind
  `IRevokedDeviceDirectory` (+1 DB read per authenticated mobile request) via a short superseding
  ADR? ADR-0026 is accepted conditional on this one ratification.
- **X1 — user-keyed session cutoff on password reset — DESIGNED + ACCEPTED as ADR-0027 (T-0418,
  panel verdict 2026-07-15, amendments U1–U3).** Reset revokes all refresh tokens
  (`ChangePassword.cs:112-113`, T-0407 landed) but outstanding access tokens rode ≤ 30 min on the
  account-takeover recovery path. ADR-0027 closes it with a sibling `RevokedUserDirectory` (option a:
  `userId`-keyed, fed from the `password_reset` rows, no migration). The ADR-0026 D9.4 exclusion is
  lifted *by ADR-0027* for the reset case; user-disable and the web path remain excluded. **T-0418 is
  now unblocked** (AC1 satisfied); backend lane picks it up after T-0414 (device machinery) lands.
- **X2 — revoke↔rotation TOCTOU hardening** (ADR-0026 D9.7) — `security_touching`, small.
- **X3 — WARN-log headerless mobile-host logins** — evidence-gates a future required-`X-Device-Id`
  login validator (would close the D9.2 claim-less residue for new sessions).
- **Web-host TTL decision** (ADR-0024 D4.3) — admin host first/separable; `security_touching`, high.
- **Mobile refresh retryable-vs-terminal classification** (ADR-0024 D4.5) — Android landed
  (`RefreshResult.Unavailable`); iOS pending; urgency trigger = first `auth` 429 in production.
- **Mint `exp` through `TimeProvider`** (hygiene) — ADR-0026's new components already use
  `TimeProvider`; the two legacy mint sites still don't.
- **Catalog edit at ADR-0026 acceptance (due now):** amend the ADR-0024 token-lifetime paragraph
  in `agents/knowledge/security-rules.md:48-50` (new ≤ 30 s bound + claim-not-header rule + pinned
  `DeviceRevocation` config); executed by the architect lane as the acceptance follow-up (outside
  the panel's writable surface).

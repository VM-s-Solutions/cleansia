---
id: T-0418
title: "Security — password RESET must lock the intruder out ~immediately: sibling RevokedUserDirectory (userId-keyed) fed from the persisted password_reset refresh-token rows fails any mobile access token whose iat predates the reset, within the ≤ 30 s poll bound (ADR-0027 / ADR-0026 X1)"
status: ready
size: M
owner: architect
created: 2026-07-15
updated: 2026-07-15
depends_on: [T-0414]
blocks: []
stories: []
adrs: [ADR-0027, ADR-0026, ADR-0024, T-0407]
layers: [backend]
security_touching: true
priority: high
manual_steps: []
sprint: 12
source: ADR-0026 verdict instruction X1 / challenger CH-13 — T-0407 makes reset revoke all refresh tokens, but the attacker's outstanding ACCESS token still rides ≤ 30 min on the account-takeover recovery path.
---

> **Extends ADR-0026 to the user dimension.** T-0407 (landed) makes password RESET revoke every
> refresh token (keep-none, reason `password_reset`) — so the attacker cannot *renew*. But the
> outstanding access JWT still rides ≤ 30 min (ADR-0024 TTL) on the exact path the owner cares about:
> the victim just reset their password to lock out an intruder. ADR-0027 (**PROPOSED — panel
> pending**) closes it exactly as ADR-0026 closed device revocation, one key over: a **sibling
> `RevokedUserDirectory`** — an in-memory `userId → resetAt` snapshot polled from Postgres every
> ≤ 30 s — is consulted in the *same* `OnTokenValidated` helper (one extra O(1) probe). A mobile token
> whose `iat` **predates** the user's most-recent reset fails with **401**, and the client's existing
> 401→refresh→refresh-rejected machinery converts that into token wipe + forced sign-out with **zero
> client change**. **No migration** (the poll reads the `password_reset` rows T-0407 already writes).
> Password CHANGE is deliberately NOT accelerated (ADR-0027 D3). The 30-min TTL stays as the fail-open
> backstop. Full rationale, the option a/b/c trade-off, failure posture, and residues: ADR-0027.

> **ADR-0027 is ACCEPTED (panel verdict 2026-07-15, amendments U1–U3).** AC1's ADR condition is
> satisfied; T-0418 is `ready` and the backend lane picks it up once T-0414 (device machinery) has
> landed. The panel's three folded amendments the backend lane must honour: **U1** — TC-REVOKE-USER-2
> gains the same-second boundary case (a post-reset re-login whose `iat` truncates into the reset's
> wall-clock second 401s once then self-heals via its live refresh token); **U4** — a Postgres-backed
> (Testcontainers) integration test on `GetPasswordResetsSinceAsync` proving the `GroupBy` +
> `Max(RevokedAt!.Value)` aggregate translates server-side (TC-REVOKE-USER-9); **U2/U3** — two named
> residues (shared kill switch D9.8; change-based recovery D9.9) — documentation, no build change.

> **Depends on T-0414.** This ticket reuses the device-revocation machinery T-0414 lands (the shared
> `OnTokenValidated` helper, the `DeviceRevocation` config section, the un-killable-refresher +
> immutable-snapshot patterns). Land T-0414 first, then this sibling slots in beside it. If the lanes
> run concurrently, coordinate merge order on the shared `OnTokenValidated` helper and the two host
> `ServiceExtensions.cs` via the PM.

## Ratified implementation instruction (backend lane — from ADR-0027 D1–D8)

**1. Repository read (poll source — no migration, reads the T-0407 rows):**
- `IRefreshTokenRepository.GetPasswordResetsSinceAsync(DateTimeOffset cutoff, CancellationToken)` →
  projection `(UserId, ResetAt)` where `ResetAt = MAX(RevokedAt)` per user, over rows with
  **`RevokedReason == "password_reset"` ALONE** (never `password_changed` — ADR-0027 D3) and
  `RevokedAt != null && RevokedAt >= cutoff`. **With `.IgnoreQueryFilters()`** + a comment citing the
  sanctioned background/cross-tenant read pattern (copy the rationale already at
  `RefreshTokenRepository.cs:15-19`/`:28-30`). No new index (rows are pruned by `DeleteStaleAsync`;
  the horizon caps the scan — ADR-0027 D9.6/D9.7).
- The literal `"password_reset"` is exactly what `ChangePassword.Handler` writes
  (`ChangePassword.cs:113`) and `RefreshToken` documents (`RefreshToken.cs:31-32`). Do NOT introduce a
  new constant that could drift; match the existing string (a shared `const` is fine if one already
  exists — do not invent divergent literals).

**2. Directory + refresher (new, `Cleansia.Config` — sibling to the device pair; CRC card
`agents/knowledge/roles/revoked-user-directory.md`):**
- `IRevokedUserDirectory.IsRevoked(string userId, DateTimeOffset? tokenIssuedAt)` — immutable snapshot
  dictionary (`userId → resetAt`), atomic reference swap, reject iff an entry exists and
  `tokenIssuedAt is null || tokenIssuedAt < resetAt`. Structural clone of `RevokedDeviceDirectory`,
  one key narrower — **reuse its `Replace` latest-wins logic** (a user reset twice inside the horizon
  keeps the later instant).
- `RevokedUserDirectoryRefresher : BackgroundService` — poll every `DeviceRevocation:RefreshSeconds`
  (default **30**, the SHARED cadence — ADR-0027 D7); horizon = `AccessTokenExpMinutes` + 5 min;
  scoped `IRefreshTokenRepository` per tick; **`TimeProvider`** for all time math; synchronous initial
  fill attempt at startup, empty-on-failure; on poll failure keep last snapshot and WARN when snapshot
  age > 3× interval (**fail-open** — ADR-0027 D5; never fail-closed).
- **The loop must be un-killable** (ADR-0026 A3 parity, non-negotiable): the entire tick body sits
  inside the `while (!stoppingToken.IsCancellationRequested)` loop's try/catch — **no exception may
  escape `ExecuteAsync`** (default `BackgroundServiceExceptionBehavior.StopHost` would crash the host;
  a dead loop can never emit the staleness warning). Copy the exact structure from
  `RevokedDeviceDirectoryRefresher.cs`.
- `AddUserRevocationEnforcement(configuration)` extension in `Cleansia.Config.Services` — registers
  the directory + refresher. **Binds NO new options** — it reads the existing `DeviceRevocation`
  section (`Enabled`, `RefreshSeconds`), the shared switch (ADR-0027 D7).

**3. Enforcement (the ONLY per-host-adjacent touch — one probe in the SHARED helper):**
- Extend the shared `OnTokenValidated` helper (`DeviceRevocationTokenValidation.cs`, or a sibling
  helper it calls — keep the logic in `Cleansia.Config` once) so that after the device probe it reads
  the already-parsed `sub` (`ClaimTypes.NameIdentifier`) + `iat` off the principal and calls
  `userDirectory.IsRevoked(userId, iat)`; a match → **`context.Fail("session_revoked")`** (→ **401**,
  never 403 — the 401 drives the client machinery; a distinct reason string keeps device vs user
  causes separable in logs).
- **The key is `sub`, which every token carries → NO claim-transition window** (ADR-0027 D4): unlike
  the `device_id` rollout, this check is fully effective from the first post-deploy request. Do NOT
  add any grace/transition handling for "tokens without the claim" — every token has `sub`.
- Honor the **same** `DeviceRevocation:Enabled` kill switch: when false, the user probe no-ops too
  (both mobile revocation checks off with one flip), but the refresher keeps polling (warm snapshot,
  live telemetry — ADR-0026 A5 parity).
- **Edge (A2 parity):** a token with `sub` matching a directory entry but a missing/unreadable `iat`
  → `context.Fail` (cannot prove it postdates the reset; both mint sites always stamp `iat`, and a
  legitimate anomaly self-heals via refresh in one round trip).

**4. Host wiring (two mobile hosts, one small edit each):**
- `Web.Mobile.Partner` + `Web.Mobile.Customer` `ServiceExtensions.AddServices`: add
  `.AddUserRevocationEnforcement(configuration)` (next to the existing
  `.AddDeviceRevocationEnforcement(configuration)`).
- **No new appsettings key** (reuses `DeviceRevocation`). **No mint-site change** (`sub`/`iat` already
  minted; `TokenService.cs` / `RefreshToken.cs` **untouched**). **Web hosts byte-untouched.** No
  Bicep, no NSwag, no client change.
- Do NOT wire `ChangeOwnPassword` (`password_changed`) into any directory feed — the poll predicate is
  `password_reset` alone (ADR-0027 D3 pinned by TC-REVOKE-USER-6).

## Test contract (TC definitions in ADR-0027 §"How a reviewer verifies compliance")
- **TC-REVOKE-USER-1** — reset a user → their EXISTING unexpired pre-reset access token → **401**
  (not 403) after a directory refresh; an unrelated user's token unaffected. (HostTests, mobile host,
  short `RefreshSeconds` or the refresher's public `RefreshOnceAsync` forced.)
- **TC-REVOKE-USER-2** — post-reset re-login (new token, `iat > resetAt`) while the directory entry is
  still present → new token **200** (the `iat` guard: reset kills sessions, not the user). **Boundary
  case (panel U1):** a post-reset re-login whose `iat` truncates into the reset's *same wall-clock
  second* (so `iat < resetAt` sub-second) 401s **once** and then **self-heals** — its refresh token
  (minted after the reset, never in the keep-none revoke set) is alive, refresh succeeds, the retried
  request **200s**. Pins "no lockout on the recovery path" as a proven property.
- **TC-REVOKE-USER-3** — two users; reset A → A's pre-reset token 401s, **B's token stays 200** across
  the refresh (`sub`-scoped, unrelated users unaffected).
- **TC-REVOKE-USER-4** — refresher repo-fake throws on *consecutive* ticks → last snapshot keeps
  serving + staleness WARN past 3× interval + the refresher still attempts the next tick (un-killable
  loop); a counting fake proves **zero repository calls on the request-path check** (the perf pin).
- **TC-REVOKE-USER-5** — directory unit contract (mirror `RevokedDeviceDirectoryTests`): token before
  reset → revoked; token after reset → passes; unreadable `iat` matching an entry → revoked (A2
  parity); a user reset twice keeps the later instant (a session minted between the two is killed).
- **TC-REVOKE-USER-6 (the D3 pin)** — authenticated `ChangeOwnPassword` for a user → force a directory
  refresh → the user's OTHER pre-change access tokens are **NOT** 401'd by the user directory, and the
  change caller's own token is untouched (password CHANGE is not accelerated; the feed is
  `password_reset` only).
- **TC-REVOKE-USER-7** — raw-file config pin (TC-REVOKE-NOW-7 mechanism): the four mobile appsettings
  carry `DeviceRevocation:Enabled: true` and `RefreshSeconds <= 30` (shared switch governs the user
  check too); the three web hosts install **neither** directory (grep-pinned in the host-wiring test).
- **TC-REVOKE-USER-8 (D6 bonus)** — a mobile login WITHOUT `X-Device-Id` (no `device_id` claim) whose
  user is then reset → its pre-reset access token 401s (keyed on `sub`, which it carries). Pins the
  narrowing of the ADR-0026 D9.2 claim-less residue for the reset case.
- **TC-REVOKE-USER-9 (panel U4 — real Postgres)** — Testcontainers integration test on
  `IRefreshTokenRepository.GetPasswordResetsSinceAsync`: seed `password_reset` rows for two users (one
  reset twice) plus `password_changed`/`logout`/out-of-horizon negatives → assert the method returns
  exactly `(UserId, MAX(RevokedAt))` for the in-horizon `password_reset` rows and omits the rest.
  Proves the `GroupBy` + `Max(RevokedAt!.Value)` aggregate over a nullable column translates
  server-side (this codebase's standing group-by/aggregate/null-projection 500 lesson) — an in-memory
  fake is NOT sufficient for this method.
- **Regression:** all TC-REVOKE-NOW-1..9 (device) and TC-REVOKE-TTL-1..5 stay green **unedited** —
  this ticket adds a sibling, it does not touch the device path or the TTL.

## Acceptance criteria
- [x] **AC1** — ADR-0027 is `accepted` (panel verdict 2026-07-15, amendments U1–U3; challenge/defense/
  verdict complete). T-0414 (device machinery) must also have landed before this lane starts
  (`depends_on`) — that gate remains.
- [ ] **AC2** — resetting a password ends that user's mobile access within `RefreshSeconds`
  (default 30 s): a pre-reset access token 401s, its refresh is rejected (`InvalidRefreshToken`, the
  chain is already `password_reset`-revoked), and the client force-signs-out — pinned by
  TC-REVOKE-USER-1, and it covers claim-less mobile sessions too — TC-REVOKE-USER-8.
- [ ] **AC3** — no false kills / correct scope: the post-reset re-login passes, unrelated users are
  untouched, and password CHANGE is NOT accelerated — pinned by TC-REVOKE-USER-2/3/6.
- [ ] **AC4** — request-path enforcement performs zero I/O; poll failure degrades open with telemetry
  (ceiling = the TTL; the reset user's refresh chain is already dead so they cannot renew), never
  fail-closed; the refresher loop is un-killable — pinned by TC-REVOKE-USER-4.
- [ ] **AC5** — `dotnet test` green including all TC-REVOKE-NOW-* and TC-REVOKE-TTL-* **unedited**,
  the TC-REVOKE-USER-2 same-second boundary pin (U1), and the Postgres-backed TC-REVOKE-USER-9 poll
  translation test (U4); no migration; web hosts and `deploy/bicep/**` byte-untouched; no mint-site
  change; no client contract change; no new appsettings key.

## Notes
- **Why option (a) sibling, not (b) fold-into-device / (c) security-stamp column** — ADR-0027 D1/Alt:
  (b) breaks the device directory's composite-key `IsRevoked` contract (two probes per device request)
  and its CRC "does NOT know Users" boundary; (c) needs a migration + a mint-site claim + a
  claim-transition window this design avoids by keying on `sub`. The `password_reset` rows T-0407
  writes ARE the timestamped stamp — no second source of truth.
- **The userId-keying objection that blocked device revocation does NOT apply** — reset is keep-none
  (kill everything is the intent), so `userId` is exactly the right key and there is no "current
  device to spare"; the one session that legitimately survives is the post-reset re-login, handled by
  the `iat` guard for free.
- **401-vs-403 is load-bearing** (same as T-0414): OkHttp's authenticator and the iOS bridge react to
  401 only — fail *authentication* in `OnTokenValidated`, not authorization.
- **The false-positive self-heal is free**: a stale-snapshot 401 against a legitimate (post-reset)
  session triggers refresh → refresh **succeeds** → retry carries a fresher `iat` → passes. Forced
  sign-out only ever follows refresh *rejection*.
- **Escalation seams (do NOT build):** literal-zero = read-through `IRevokedUserDirectory` impl
  (inherits ADR-0026's open owner "is ≤ 30 s immediate?" question — do not re-ask it here);
  user-disable (`IsActive = false`) cutoff = a second feed into this same directory under its own
  superseding ADR.
- **Explicitly NOT this ticket's scope:** password CHANGE acceleration (ADR-0027 D3 non-goal — a
  separate decision that must first answer the spared-session 401); web-host reset cutoff (rides the
  standing web-host TTL follow-up, ADR-0024 D4.3, admin-first/separable); user-disable acceleration.
- **Standing rule (ADR-0027 D9.6):** any future *bulk* `password_reset` write path (mass forced-reset)
  inflates this snapshot and triggers a fleet-wide silent-refresh ripple — check it against ADR-0027
  before shipping.
- **Accepted residues from the panel (documentation only — no build change):** **D9.8** — the
  `DeviceRevocation:Enabled` switch is shared across the device and user checks and cannot express
  "device off, reset cutoff on" (a distinct `UserRevocation:Enabled` is the named split); **D9.9** — a
  not-fully-locked-out victim who recovers via in-app `ChangeOwnPassword` (not the email RESET) leaves
  the attacker's access token alive ≤ 30 min (change is deliberately not fed into the directory).

## Status log
- 2026-07-15 — filed `proposed` by the architect (author mode) alongside ADR-0027 (proposed). Awaiting
  the challenger/lead panel on the ADR before the backend lane picks this up. Depends on T-0414
  (device machinery) landing first.
- 2026-07-15 — architect panel (challenger→lead) **accepted ADR-0027** with amendments U1–U3; AC1
  satisfied → ticket moved to `ready`. Two test additions the backend lane must land: TC-REVOKE-USER-2
  same-second boundary self-heal (U1) and the Postgres-backed TC-REVOKE-USER-9 poll-translation test
  (U4). Still gated on T-0414 landing first (`depends_on`).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->

# Auth sessions — token lifetimes, device revocation, forced sign-out

**Status:** Accepted (ADR-0024, panel verdict 2026-07-15 — amendments A1–A5 folded in; D1 stands:
30 min on the two mobile hosts, config-only). Canonical decision:
`agents/backlog/adr/0024-mobile-access-token-ttl-is-the-device-revocation-latency-bound.md`. This
page is the evolving companion; the ADR governs on any conflict. Prior related security notes:
`agents/backlog/security/auth-sessions.md` (tenant-filter symmetry on token reads, T-0236).

## The decision in one paragraph
Revoking a device now has a documented latency bound: **the access-token TTL of the mobile host
that issued the session**, and that TTL drops **1440 → 30 min** on `Web.Mobile.Partner` and
`Web.Mobile.Customer` (config-only; four appsettings files). Revocation already correctly kills the
device's refresh tokens (`RevokeDevice` → `RevokeByDeviceAsync`, device-id carried across
rotation); the outstanding JWT was the gap — it lived 24 h. With a 30-min TTL the next
401-triggered silent refresh after expiry fails (`InvalidRefreshToken`) and both mobile clients
wipe tokens/caches and force sign-out. Per-request session checks (option B) and push-driven
force-logout (option C) are deferred escalations with named triggers, not adopted.

## The current shape (once ADR-0024 lands)

| Piece | Value / mechanism | Where |
|---|---|---|
| Mobile access-token TTL | **30 min** (= the revocation bound; ClockSkew=Zero on all hosts) | mobile hosts' `appsettings*.json` `JwtSettings:AccessTokenExpMinutes` |
| Web access-token TTL | 1440 min — **known finding, follow-up decision pending** (admin SPA separable/first; customer gated on SSR/cookie refresh-path verification + T-0400) | web hosts' `appsettings*.json` |
| Refresh-token TTL | 30 d (rememberMe) / 1 d, sliding on rotation | `RefreshTokenService.Issue/RotateAsync` |
| Revocation write path | device row deactivated + all its refresh tokens revoked (`device_revoked`); rotation-reuse → chain revoke | `RevokeDevice.cs:43-44`, `RefreshTokenService.cs:57-71,120-133` |
| Revocation read path | none per-request (no jti/denylist/stamp) — **by decision**; the TTL is the bound | ADR-0024 D2/D3 |
| Mobile silent refresh | 401-reactive, single-flight, forced sign-out on refresh rejection | Android `AuthAuthenticator.kt`; iOS `SessionRefresher.swift` + `GeneratedClientAuthBridge.swift` |
| Web silent refresh | 401-reactive interceptor + coordinator over HttpOnly cookies | `error.interceptor.ts` per app; `AuthCookieWriter.cs` |

## Rules this locks in
- **`AccessTokenExpMinutes` on a mobile host is a security bound, not a tuning knob.** Changing it
  requires a superseding ADR. Pinned by TC-REVOKE-TTL-4 — **a raw-file JSON pin, not a bound-config
  HostTests assertion** (the HostTests overlay layers its own 15-min value last, so a bound pin can
  never see 30 — panel amendment A5). The same test pins the three web hosts at 1440, so silent
  scope creep also fails a test until the follow-up ADR supersedes it.
- **Revocation latency claims are stated as "≤ mobile TTL".** Anyone promising "instant" is
  describing option B/C, which are not built.
- **Web sessions cannot be device-revoked** — browsers never send `X-Device-Id`, so their refresh
  tokens carry `DeviceId = null` and the deliberate null-guard (`RefreshTokenService.cs:129`) skips
  them. Do not "fix" the null-guard; it protects legacy tokens from cross-device revocation.

## Trade-off space (kept live for the next revisit)
- **A (chosen): short TTL.** Config-only; bound = TTL; cost = ~2 silent refreshes per active hour.
- **B: per-request device/session check** (cached `Device.IsActive` or jti denylist). Gets
  near-instant kill; costs a per-request read + a cache-invalidation seam. **Trigger:** an incident
  where ≤30 min residual access caused harm, or a product/compliance demand for instant revocation.
  B composes with A (never replaces it — a stale cache over a 24 h JWT fails open).
- **C: push-driven force-logout** on `device_revoked`. UX only — attacker-suppressible, so never a
  substitute for A. **Trigger:** iOS push receive-side landing (T-0403/T-0404); then it's a
  `NotificationEventCatalog` event + client handling.

## Watch items (post-rollout)
- **Spurious forced sign-outs:** both mobile clients conflate refresh transport failures (and 429s)
  with rejection and sign out (`AuthAuthenticator.kt:75-87`; `Auth.swift:221-239` →
  `SessionRefresher`). Exercised ~50× more often at 30 min. **Upgraded by the panel (ADR-0024 D4.5,
  amendment A4) from watch item to a filed-at-acceptance client ticket**: classify retryable
  (transport, 429, 5xx) vs terminal (401/400 rejection); on retryable, fail the request without
  wiping tokens or emitting ForcedSignOut. Urgency trigger: first `auth`-policy 429 in the D8
  counter. Never fix by TTL relaxation.
- **`auth` rate-limit bucket (ADR-0003):** refresh is anonymous → per-IP 10/min **fixed 60 s window,
  `QueueLimit = 0` — the binding constraint is the burst, not the average** (panel amendment A2):
  a synchronized wake after an idle gap > 30 min (shift start on shared wifi) hits the cap at ~10
  co-located devices; the steady-state ~300/IP figure only holds for uniformly spread refreshes.
  Watch the D8 rejection counter; `RateLimiting:Auth:AnonPermitLimit` is the same-day config relief
  valve.
- **`RefreshTokens` row growth:** ~16 rows/day per 8h-active device; `DeleteStaleAsync` covers it.
- **T-0406:** Android partner lacks the forced-signout *collector* (token wipe works; UI parks).
  UX-only, ticketed.

## Open follow-ups (as ratified — ADR-0024 Verdict "Actions handed to the PM")
- **Web-host TTL decision** (ADR-0024 D4.3, sharpened by amendment A3): **the admin host is the
  priority and is separable** — the admin SPA's 401→refresh interceptor path has no SSR complexity
  and must not wait on the customer-SSR verification (rotating single-use refresh cookie vs SSR
  replay; `Set-Cookie` propagation; T-0400 interplay). Inherits `security_touching: true`, high
  priority.
- **Mobile refresh retryable-vs-terminal classification** (ADR-0024 D4.5) — Android + iOS; filed at
  acceptance; urgency trigger = first `auth` 429 in production telemetry.
- **Password change/reset revokes other refresh tokens** (ADR-0024 D4.6 — panel finding:
  `ChangePassword.cs` revokes nothing; device revoke is the account holder's only kill switch
  today). Separate decision; until it lands the compromise playbook is *revoke device / admin
  disable*, not password change.
- **Mint `exp` through `TimeProvider`** (hygiene — `TokenService.cs:74` / `RefreshToken.cs:125` use
  raw `DateTime.UtcNow`; `TokenService` already injects `TimeProvider` and ignores it for expiry).
  Enables an exact-boundary expiry test; until then TC-REVOKE-TTL-2 uses the fractional-TTL boot
  mechanism.
- **Catalog edit (confirmed by the Verdict):** one line into `agents/knowledge/security-rules.md`
  near S2 — *"the access-token TTL on a host that issues device-bound sessions is a security bound —
  changing it requires an ADR (ADR-0024)"*. Executed by the architect lane (outside the panel's
  writable surface).

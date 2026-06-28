# Security findings — iOS APNs push registration (Device/Register + logout-clear)

## 2026-06-28 — T-0311 APNs push gate (Gate-SEC, security reviewer) — PASS-the-design (binding rules)

**security_touching: YES.** T-0311 adds a **device-token write surface** (`/api/Device/Register`) and
makes the **logout-clear** of that token a real session-security property (a logged-out handset must stop
receiving pushes). Both are security-relevant; this ticket goes through Gate-SEC.

**Verdict: PASS-the-design with FOUR binding implementation rules + ONE required test.** Scope = the
**registration authz / device-id / logout-clear / token-handling** security gate of T-0311. The
ARCHITECT rules the seam / lifecycle-home (where registration is hung) / foreground-permission flow in
parallel — this note stays out of those. T-0311 is **greenfield on iOS** — `CleansiaCore/.../Push/Push.swift`
is a bare `public enum Push {}` placeholder and there is **no** `deviceRegister`/`deviceUnregister` call
site, no APNs delegate, no last-token cache on disk yet. So these are rules the iOS developer builds to and
the reviewer enforces, not findings against shipped iOS code. The **backend Device/Register + Device/Unregister
were traced on this Mac and are server-scoped + safe (DECISION 2 + the Unregister half of DECISION 3 VERIFIED,
not flagged).** Composes with the T-0310 Devices gate (`security/ios-devices.md`, D6–D8): the registered row
surfaces in `Device/Mine` under the same `deviceId`, carrying no token to the UI (S4, re-confirmed below).

### What was read (trace base)
- Backend (reachable, traced): `Core.AppServices/Features/Devices/{RegisterDevice,UnregisterDevice,
  RevokeDevice,GetMyDevices}.cs` + `DTOs/DeviceDto.cs` + `Mappers/DeviceMapper.cs`;
  `Infra.Database/Repositories/DeviceRepository.cs`; `Infra.Database/BaseRepository.cs:122-125` (`Deactivate`);
  `Infra.Database/EntityConfigurations/DeviceConfiguration.cs`; `Domain/Devices/Device.cs`;
  controllers `Web.Mobile.Partner/Controllers/DeviceController.cs` + `Web.Mobile.Customer/Controllers/DeviceController.cs`.
- The delivery side (proves Deactivate stops pushes): `Functions.Core/Handlers/SendPushNotificationHandler.cs:121-134`.
- iOS spine (T-0311 rides it): `CleansiaCore/.../Auth/{Auth,SessionScopedCache,SessionRefresher}.swift`;
  the push placeholder `CleansiaCore/.../Push/Push.swift`.
- Android parity (the build-to reference): `core/notifications/{PushTokenSessionObserver,PushTokenRepository}.kt`;
  `partner-app/.../data/auth/AuthRepository.kt:210-231` (the logout ordering).
- Backend tests: `Cleansia.Tests/Features/Devices/UnregisterDeviceHandlerTests.cs`.

### S1–S10 walk (the diff = the T-0311 iOS register/logout client + the backend Device write surface)

- **S1 (userId is server-truth) — PASS.** `RegisterDevice.Handler` (`RegisterDevice.cs:35`) and
  `UnregisterDevice.Handler` (`UnregisterDevice.cs:28`) both derive `userId` from
  `IUserSessionProvider.GetUserId()` and bind the row to it; empty session → `UserNotFound` failure
  (`RegisterDevice.cs:36-40`, `UnregisterDevice.cs:29-33`). The command carries **NO** UserId / TenantId /
  email field — only `DeviceId`, `DeviceToken`, `Platform` (register) and `DeviceId` (unregister). The
  caller cannot register or unregister against another user's account by sending an id in the body.
- **S2 (authorization) — PASS.** `Device/Register` (`[HttpPost("Register")]`) and `Device/Unregister`
  (`[HttpDelete("Unregister")]`) are both `[Permission(Policy.Authenticated)]` on BOTH the partner
  (`Web.Mobile.Partner/Controllers/DeviceController.cs:17,29`) and customer
  (`Web.Mobile.Customer/Controllers/DeviceController.cs:17,29`) hosts. **Not** `[AllowAnonymous]`, not a
  missing attribute. Confirmed `/api/Device/Register` is NOT in the iOS partner `AnonymousAllowList`
  (only `/api/auth/*` + `/api/user/password*` per the T-0310 trace) → the `HeaderAdapter` stamps the
  **Bearer** on it → **authed**, as CONTEXT asserts.
- **S3 (resource-by-id ownership) — PASS (load-bearing).** Register's upsert reads via
  `GetByUserAndDeviceIdAsync(userId, DeviceId)` (`RegisterDevice.cs:42` → `DeviceRepository.cs:15-19`,
  `WHERE UserId == userId && DeviceId == deviceId && IsActive`). A foreign `deviceId` (one already owned by
  user B) is invisible to user A's lookup → A takes the **insert** branch and creates A's OWN row; A can
  **NOT** overwrite B's token / hijack B's row. The `(UserId, DeviceId)` unique index
  (`DeviceConfiguration.cs:35`) is what makes that insert safe (per-user, not global — the documented
  account-switch fix). Unregister scopes identically (`UnregisterDevice.cs:35`): a `deviceId` not owned by
  the caller → `device is null` → no-op success, never touches another user's row.
- **S4 (DTO leak) — PASS (re-confirmed from T-0310).** The register/unregister responses are
  `RegisterDevice.Response(string DeviceId)` (the row id) and `UnregisterDevice.Response(bool Success)` —
  **no token echoed back.** `DeviceDto` (the `Device/Mine` shape T-0311 rows show up in) is
  `Id, Platform, DeviceId, LastActiveAt, IsCurrent` only — **no `DeviceToken`, no `UserId`, no `TenantId`**
  (`DeviceDto.cs`, `DeviceMapper.cs`). The APNs push secret never leaves the server.
- **S5 (rate limiting) — PASS.** `Device/Register` and `Device/Unregister` both carry
  `[EnableRateLimiting("auth")]` on both hosts (`DeviceController.cs:18,30`) — the partitioned shared window
  (per-IP anon / per-`sub` authed). The token-write side-effect is throttled; a token-churn loop is bounded.
- **S6 (logging hygiene) — PASS server-side; BINDING on iOS (RULE 4).** Backend sweep for `DeviceToken` in
  any `log*`/Sentry call = **clean** (the `TrustedDeviceToken` hits are the unrelated remember-me token, none
  are log calls); `SendPushNotificationHandler` logs `{UserId}`/`{EventKey}`/counts, never the token, never the
  raw body (`:59,75,128,186`). The APNs token is device-scoped (not a user secret) but is still a delivery
  credential — **the iOS client must never log it** (incl. Sentry/crash capture). See RULE 4.
- **S7 (idempotency) — PASS.** Re-register with the same `(userId, deviceId)` is the **upsert** path:
  `existingDevice.UpdateToken(...)` (`RegisterDevice.cs:46`), no duplicate row — the `PushTokenSessionObserver`
  parity (`ensureRegistered` fires on every session×token pair and is "free" when unchanged). Re-unregister
  of an already-deactivated row → `GetByUserAndDeviceIdAsync` filters `IsActive` → returns null → no-op
  success (`UnregisterDeviceHandlerTests.Unregistering_Missing_Device_Is_A_Noop_And_Still_Succeeds`).
  Register has no doublable financial/email side effect.
- **S8 (tenant isolation) — PASS today; LATENT multi-tenant dependency (standing, unchanged).** `Device` is
  `ITenantEntity` (`Device.cs:6`); the `(UserId, DeviceId)` unique index is correctly the composite, not
  `DeviceId` alone (`DeviceConfiguration.cs:35`). The global tenant filter applies on top of the explicit
  `UserId` scope on every Device read. **Standing note (NOT a T-0311 regression):** the same RefreshToken
  tenant read-asymmetry the device-revoke kill rides on (`auth-sessions.md` / `ios-devices.md` S8) applies to
  any future non-null-`TenantId` onboarding — dormant in single-tenant prod. Register itself is authed (JWT
  carries `tenant_id`), so its write is correctly tenant-stamped; no new asymmetry introduced here.
- **S9 (migration/DTO contract) — N/A.** No schema change; `Device/Register`/`Device/Unregister` already
  ship in the mobile spec. The iOS client is generated, not hand-edited (owner `nswag/spec-regen` manual step
  unchanged). The `RegisterDeviceCommand{deviceId, deviceToken, platform}` shape is unchanged.
- **S10 (soft-delete) — PASS (load-bearing — this is the logout-clear guarantee).** Unregister calls
  `deviceRepository.Deactivate(device)` (`UnregisterDevice.cs:39`), which sets `IsActive = false`
  (`BaseRepository.cs:122-125`) — a **soft-delete, never a hard remove**
  (`UnregisterDeviceHandlerTests.Unregistering_Existing_Device_Soft_Deletes_And_Never_Hard_Removes`). The push
  dispatcher fetches eligible devices via `deviceRepository.GetByUserIdAsync(userId)`
  (`SendPushNotificationHandler.cs:121`), which filters `&& d.IsActive` (`DeviceRepository.cs:30`). So a
  deactivated (unregistered) row is **excluded from the delivery set** → APNs delivery to that handset stops.
  **This is the chain that makes "logged-out handset stops receiving pushes" true**, and it depends on the
  unregister actually firing (RULE 3).

### Binding rules (the iOS developer builds to these; the reviewer enforces them)

**RULE 1 — spine-authed registration on the single device-id (BINDING).** `Device/Register` MUST be called
through the ADR-0019 spine (Bearer + `X-Device-Id`) — it is NOT anon-allow-listed. The `deviceId` sent in
the `RegisterDeviceCommand` MUST be `DeviceIdProvider.deviceId` — the **same** mint-once instance the
`HeaderAdapter` stamps as `X-Device-Id` and that T-0310's `deviceMine` uses (the D6 single source). **No
second id, no per-call `UUID()`, no `identifierForVendor`, no alternate Keychain account.** *Why load-bearing:*
the registered row must collide with the T-0310 Devices list and the revoke kill on the same `deviceId`; a
second id source forks the identity → the row the user sees and revokes is not the row receiving pushes.
*Reviewer grep:* the only expression feeding the register command's `deviceId` (and `unregister`'s) is the
injected `DeviceIdProvider.deviceId`; a literal / fresh `UUID` / `identifierForVendor` is a FAIL. `platform`
MUST be the literal `"ios"` (the backend validator rejects anything but `android`/`ios` — `RegisterDevice.cs:24`).

**RULE 2 — register on session × token, never store the token unauthenticated (BINDING).** Mirror the Android
`PushTokenSessionObserver`: registration is a **property of session state**, not a one-shot login event —
register on each distinct (authenticated-session × APNs-token) pair (login, cold-launch-with-session,
APNs-token-rotation-while-signed-in; a rotation while signed-out is buffered until the session returns). Do
NOT register while signed out (no Bearer → the call 401s and the row is never written). The token cache is
acceptable in `UserDefaults` (RULE 4b) but registration to the server only happens with a live session.

**RULE 3 — logout MUST unregister BEFORE the token is wiped, and the cache MUST clear on ALL sign-outs
(BINDING — the load-bearing session-security property; require BOTH halves).**
  - **(a) Explicit user logout:** call `Device/Unregister` (the authed DELETE) **BEFORE** `tokenStore.clear()`
    — so the Bearer is still present and the DELETE actually deletes the row (server-side `Deactivate` →
    delivery stops, S10 chain above). This is the Android `AuthRepository.kt:210-225` ordering verbatim
    (`runCatching { pushTokenRepository.unregisterDevice() }` first, THEN wipe). **Best-effort:** an
    unregister failure still proceeds to the local wipe (the row is GC'd server-side by the dead-token prune
    when APNs next reports the token invalid). *Why ordering is non-negotiable:* iOS `Auth.logout()`
    (`Auth.swift:180-185`) today calls only `api/Auth/Logout` then `signOutLocal()` → `tokenStore.clear()`;
    there is **no** push-unregister in the path (because T-0311 isn't built). If the unregister were added
    AFTER the wipe it would 401 and the row would survive → the handset keeps receiving pushes after logout —
    the exact leak this gate exists to block.
  - **(b) Clear the local last-token cache on EVERY sign-out (user + forced).** Implement the iOS push
    last-token cache as a `SessionScopedCache` registered in the `SessionScopedCacheRegistry`. Then
    `Auth.signOutLocal()` (`Auth.swift:187-190`, `await sessionScopedCaches.clearAll()`) AND
    `SessionRefresher.forceSignOut()` (`SessionRefresher.swift:75-79`, also `clearAll()` on a
    refresh-expired/401) both clear it **for free** — covering the forced-signout path that canNOT call the
    authed unregister (the token is already dead). *Why this closes the account-switch leak:* login B after
    logout A on the same handset → A's server row is gone (3a) AND A's cached token is gone (3b), so B's
    register writes a fresh A-independent row keyed `(userB, sameDeviceId)`; B does NOT inherit A's pushes.
    The single device-id (RULE 1) + clear-on-logout (3a/3b) is what guarantees the no-inheritance property.
    *Reviewer check:* the push last-token cache conforms to `SessionScopedCache` and is `register(_:)`-ed in
    the registry; a cache that is NOT session-scoped (or that survives `clearAll()`) is a FAIL.

**RULE 4 — token handling (S6/S4) (BINDING).**
  - **(a) NO token logging anywhere** — never the APNs token in any `print`/`os_log`/`Logger`/Sentry/crash
    capture. Mirror the Android contract: log the **failure message only**, never the token value. (Backend
    is already clean — verified.)
  - **(b) `UserDefaults` is acceptable** for the last-token cache. The APNs token is device-scoped, rotates,
    is reset on reinstall, and is NOT a user secret — Keychain is not required (and `UserDefaults` is what the
    `SessionScopedCache` clear in RULE 3b operates on). Do NOT, however, cache the token in a location that
    survives `SessionScopedCacheRegistry.clearAll()`.
  - **(c) No token in the `Device/Mine` DTO** — already vetted in T-0310 (S4): `DeviceDto` carries no
    `DeviceToken`. T-0311 adds rows to that list but does NOT widen the DTO; the reviewer re-confirms the
    generated iOS `DeviceDto` still has no token field after any spec regen.

### DECISION 2 — registration authz + the spine — VERIFIED on the backend (not flagged)
Confirmed above (S1/S2/S3): `RegisterDevice.Handler`/`UnregisterDevice.Handler` derive the user from the JWT
session, bind the row to the caller, scope every lookup by `UserId`, and reject empty sessions; a foreign
`deviceId` cannot hijack or register to another account (per-`(UserId, DeviceId)` upsert + composite unique
index); `/api/Device/Register` is authed on both hosts. The registrar must NOT mint a second id (RULE 1).

### DECISION 3 — logout-clear — Unregister half VERIFIED on the backend; ordering is the iOS BINDING (RULE 3)
The **backend Unregister deletes/deactivates the row caller-scoped** (`Deactivate` → `IsActive=false`,
caller-scoped via `GetByUserAndDeviceIdAsync`, soft-delete-not-hard-remove) AND that **stops APNs delivery**
(dispatcher filters `IsActive`) — VERIFIED. The remaining guarantee — that the client calls Unregister
**before** wiping the token, and clears the cache on **all** sign-outs — is the iOS-side BINDING (RULE 3),
greenfield, enforced via the required test below.

### Required test (Gate 6)
- **TC-IOS-PUSH-LOGOUT-CLEARS (red-first):** on user logout, the push unregister
  (`Device/Unregister`) is invoked **before** the access/refresh token is wiped (assert call ordering against
  a fake auth/push client — the unregister sees a non-empty Bearer / runs while `tokenStore.current() != nil`),
  AND the push last-token cache is cleared on sign-out (assert the `SessionScopedCache.clear()` ran on BOTH the
  explicit-logout and the forced-signout paths). A unit/VM test mirroring the Android logout-ordering coverage.
- (Parity tests to port: register-on-session×token fires once per distinct pair; register short-circuits when
  the token is unchanged; rotation-while-signed-out buffers then fires on session return — these are
  functional, not the security test above.)

### Open follow-up for the backend owner
- **None new for T-0311.** Backend Register/Unregister/dispatch are server-scoped + safe as traced. The only
  standing backend item is the **latent multi-tenant S8** (RefreshToken tenant read asymmetry) already filed
  in `auth-sessions.md` / `ios-devices.md` — re-verify before any non-null-`TenantId` onboarding. The owner's
  T-0311 dependency is the **APNs auth key/cert** (infra provisioning, not a code gate); until it's set, the
  dispatcher's `result.Skipped` no-op path (`SendPushNotificationHandler.cs:149-155`) safely ACKs.

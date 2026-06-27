# Security findings — iOS partner Devices surface (Device/Mine list + revoke)

## 2026-06-27 — T-0310 Devices gate (Gate-SEC, security reviewer) — APPROVE-the-design (binding rules)

**Verdict: APPROVE-the-design with TWO BINDING implementation rules + ONE required test.** Scope = the
**device-id / revoke** security gate of T-0310 (the architect rules nav/state/settings — §7.7 D1–D5 +
scope A/B — in parallel; this note rules only decisions 6–8). T-0310 is **greenfield** — no
`deviceMine`/`deviceRevoke` call site exists on disk yet — so these are rules the developer builds to and
the reviewer enforces, not findings against shipped code. The **backend Device surface was traced on this
Mac and is server-scoped + safe** (DECISION 8 VERIFIED, not flagged). Cross-ref: sprint-12 §7.7 "Decisions
6–8" sub-note.

### What was read (trace base)
- iOS spine: `src/cleansia_ios/CleansiaCore/Sources/CleansiaCore/Auth/{DeviceIdProvider,HeaderAdapter,
  AnonymousAllowList,GeneratedClientAuthBridge,Auth}.swift`; the factory install
  `CleansiaPartner/Sources/Generated/PartnerCoreSpineRequestBuilderFactory.swift`; the one-provider wiring
  `CleansiaPartner/Sources/PartnerClients.swift:15`.
- Generated client: `CleansiaPartnerApi/APIs/PartnerDeviceAPI.swift` (`deviceMine`/`deviceRevoke`) +
  `Models/DeviceDto.swift`.
- Header-parity contract: `src/cleansia_ios/docs/header-parity-contract.md` §1/§2 (the X-Device-Id invariant).
- Android parity: `partner-app/.../features/devices/{DevicesScreen,DevicesViewModel}.kt` +
  `core/devices/DevicesRepository.kt` + `features/devices/DevicesViewModelTest.kt`.
- Backend (reachable): `Cleansia.Web.Mobile.Partner/Controllers/DeviceController.cs`;
  `Core.AppServices/Features/Devices/{RevokeDevice,GetMyDevices}.cs` + `DTOs/DeviceDto.cs` +
  `Mappers/DeviceMapper.cs`; `Infra.Database/Repositories/DeviceRepository.cs`; `Domain/Devices/Device.cs`;
  `Core.AppServices/Services/RefreshTokenService.cs:120-133` (`RevokeByDeviceAsync`).

### S1–S10 walk (the diff = the T-0310 Devices iOS client + the backend it calls)

- **S1 (userId is server-truth) — PASS.** `RevokeDevice.Handler` and `GetMyDevices.Handler` both derive
  `userId` from `IUserSessionProvider.GetUserId()` (`RevokeDevice.cs:33`, `GetMyDevices.cs:19`); no id comes
  from the body/query. The only client-supplied value on the read is `currentDeviceId`, used **solely** to
  compute the cosmetic `isCurrent` flag (`DeviceMapper.cs:14`) — never for authz scoping.
- **S2 (authorization) — PASS.** Every Device endpoint is `[Permission(Policy.Authenticated)]`
  (`DeviceController.cs:41,52`) — no `[AllowAnonymous]`, no missing attribute. The iOS paths
  `/api/Device/Mine` and `/api/Device/{id}` are **NOT** in `AnonymousAllowList` (only `/api/auth/*` +
  `/api/user/password*`), so the `HeaderAdapter` correctly **stamps the Bearer** on them.
- **S3 (resource-by-id ownership) — PASS (load-bearing).** `RevokeDevice` loads the row via
  `GetByIdAndUserAsync(DeviceRowId, userId)` (`DeviceRepository.cs:21-25`, `WHERE Id == id && UserId ==
  userId && IsActive`). A cross-user / unknown row → `device is null` → `DeviceNotFound` (`RevokeDevice.cs:38-41`)
  — **NotFound, not Forbidden**, so existence is not leaked. A partner cannot revoke another user's device.
- **S4 (DTO leak) — PASS.** `DeviceDto` exposes `Id, Platform, DeviceId, LastActiveAt, IsCurrent` only — **no
  `UserId`, no `TenantId`, no `DeviceToken`** (the push secret stays server-side). The list is the caller's own
  devices only (`GetByUserIdAsync(userId)`). `platform`/`lastActiveAt` are minimal, non-sensitive self-metadata.
- **S5 (rate limiting) — PASS.** All Device endpoints carry `[EnableRateLimiting("auth")]`
  (`DeviceController.cs:42,53`) — the partitioned shared window. The revoke side-effect is throttled.
- **S6 (logging) — N/A.** No PII logged on these paths in the traced handlers; iOS adds none.
- **S7 (idempotency) — PASS.** Re-revoking an already-revoked row: the row is `IsActive == false`, so
  `GetByIdAndUserAsync` (filters `IsActive`) returns null → `DeviceNotFound`; the side-effect
  (`RevokeByDeviceAsync`) ran once and re-running it is a no-op (only active tokens match). The iOS list
  drops the row on success (the `DevicesViewModel.kt:73-75` filter-out parity), so a stale double-tap can't
  re-hit it. Safe.
- **S8 (tenant isolation) — PASS today; LATENT multi-tenant dependency (see standing note below).** `Device`
  is `ITenantEntity` (`Device.cs:6`); the global tenant filter applies on top of the explicit `UserId` scope.
  **Standing dependency:** the revoke session-kill goes through `RefreshTokenService.RevokeByDeviceAsync` →
  `GetActiveByUserIdAsync` — the exact path flagged in `auth-sessions.md` (2026-06-10) as a latent
  anonymous-write / authenticated-read tenant asymmetry. Dormant in single-tenant prod; must be closed
  before a tenant carries non-null `User.TenantId`, or remote device-revoke silently matches zero tokens.
  Not a T-0310 regression — pre-existing class, correct in today's null-TenantId mode.
- **S9 (migration/DTO contract) — N/A.** No schema change; the spec carrying `Device/Mine` + `Device/{id}`
  is the owner's mobile-spec regen (already landed, §7.1).
- **S10 (soft-delete filter) — PASS.** All Device reads filter `&& d.IsActive` (`DeviceRepository.cs:12,18,24,30`),
  so revoked (deactivated) rows are hidden from the list.

### Binding rules (the developer builds to these; the reviewer enforces them)

**DECISION 6 — the device-id invariant (BINDING).** `deviceMine(currentDeviceId:)`'s argument MUST be
`DeviceIdProvider.deviceId` — the **same** instance the `HeaderAdapter` stamps as `X-Device-Id`
(`HeaderAdapter.swift:40`) and that `Device/Register` persisted (header-parity-contract §2). One provider per
app, wired at `PartnerClients.swift:15`. **No second source, no per-call `UUID()`, no `identifierForVendor`,
no alternate Keychain account** (the T-0331 mint-once invariant). The iOS client must route the id through a
`DevicesRepository.currentDeviceId { deviceIdProvider.deviceId }` accessor — the Android `DevicesRepository.kt:31,34`
parity. *Why load-bearing:* the server sets `isCurrent` by matching this arg against `Device.DeviceId`
(`DeviceMapper.cs:14`); drift → the caller's own row reads `isCurrent == false` → the revoke trash appears on
it → self-revoke. *Reviewer grep:* the only expression feeding `deviceMine`'s `currentDeviceId:` is the
injected `DeviceIdProvider.deviceId`; a literal / fresh `UUID` / any non-provider source is a FAIL.

**DECISION 7 — current-device-revoke → sign-out (BINDING; require BOTH).** **(a) Hide** the revoke control on
the current row — render the trash **only when `!device.isCurrent`** (the `DevicesScreen.kt:235` parity; the
current row shows a "this device" chip, `:221-224`), so self-revoke is not UI-reachable. **(b) Defensive
sign-out branch:** if a revoke **ever** targets the current device, force `authClient.logout()` and return to
the login/splash root — never leave a revoked-but-logged-in session. **Detection = the revoked row's
`deviceId == DeviceIdProvider.deviceId` (primary) OR `isCurrent == true` (secondary).** Match on either → sign
out. *Why (b) is non-optional:* the server revoke kills the refresh-token chain for that `DeviceId`
(`RevokeDevice.cs:44` → `RefreshTokenService.RevokeByDeviceAsync` `:120-132`), but the in-memory access token
keeps working until its ~15-min expiry (`Auth.swift:292`) — without (b), a self-revoke (via a future bug, a
stale-`isCurrent` race, or a direct API call) strands the partner on a dead session until the next 401. (a) is
the parity guard; (b) is the safety net that holds if (a) is bypassed.

**DECISION 8 — server-scoping of revoke (S2/S3) — VERIFIED on the backend (not flagged).** Confirmed above: the
handler derives `userId` from the JWT and scopes the row to the caller; cross-user → `DeviceNotFound`. The iOS
client adds **no** client-side ownership check beyond this and **MUST never send a `deviceRowId` it did not
receive from its own `deviceMine()` response.**

### Required test (Gate 6)
- **TC-IOS-DEVICES-SELF-REVOKE (red-first):** revoking a row whose `deviceId == DeviceIdProvider.deviceId`
  (or `isCurrent == true`) drives `authClient.logout()` + a return to the login root. A unit/VM test.
- (Parity tests to port from `DevicesViewModelTest.kt`: load→Loaded, revoke-success-removes-row, revoke-failure-keeps-list,
  re-entry-guard — these are functional, not the security test above.)

### Open follow-up for the backend owner
- **None new for T-0310.** The only backend item is the **standing latent S8** in `auth-sessions.md`
  (RefreshToken tenant stamping/read asymmetry on `GetActiveByUserIdAsync`) — already filed; flag it as the
  multi-tenant gate the device-revoke kill rides on. Re-verify it is closed before onboarding any non-null-tenant user.

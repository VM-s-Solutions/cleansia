---
id: T-0331
title: iOS DeviceIdProvider — persist own generated UUID (IDFV as seed only) + verify Keychain write OSStatus before caching
status: draft
size: S
owner: pm
created: 2026-06-26
updated: 2026-06-26
depends_on: [T-0300]
blocks: []
stories: []
adrs: [0013]
layers: [ios]
security_touching: false
manual_steps: []
sprint: 12
source: AUDIT-2026-06-26-ios-phase0-foundation F1
---

> **Deferred — NOT to be implemented now.** Logged from the 2026-06-26 adversarially-verified iOS Phase 0
> foundation audit (F1). **Low / latent severity, fully dormant** — no shipping screen exercises the
> `DeviceIdProvider` device-registration path yet (only the auth spine + its tests reference it). To be
> fixed via the normal workflow as part of the auth wave (its suggested home is the T-0300 auth spine /
> T-0303 partner-login vertical — fold in when that code path is wired). **No-decision note (panel
> skipped):** robustness + contract-compliance fix bringing the code in line with the **already-ratified**
> `header-parity-contract.md` §2 invariant (ADR-0013 §D4.4 in force, untouched); no new behavior or
> architectural decision.

## Context

The 2026-06-26 iOS Phase 0 foundation audit (`audits/AUDIT-2026-06-26-ios-phase0-foundation.md`, F1) found
two coupled robustness/contract-compliance defects on the hand-written device-id source. Both break the
`X-Device-Id` == `Device/Register` `deviceId` string-match invariant that remote device-revoke depends on —
**"the single most breakable rule on the whole auth surface"** (`src/cleansia_ios/docs/header-parity-contract.md`
§2, lines 58 + 95-96):

1. **IDFV persisted as the id.** `DeviceIdProvider.swift:42` stores `fallbackVendorId() ?? UUID().uuidString`
   — i.e. it persists `identifierForVendor` (IDFV) **as the actual `X-Device-Id` value** whenever IDFV is
   available, only minting a fresh UUID when IDFV is `nil`. The contract requires the opposite: "persist
   your own generated UUID and treat IDFV only as an **optional seed**" — IDFV "is not a safe substitute on
   its own — it can change when all vendor apps are uninstalled."
2. **Keychain write status discarded.** `KeychainStore.write` (`KeychainTokenStore.swift:99-112`) returns
   `Void` and ignores the `SecItemUpdate`/`SecItemAdd` `OSStatus`. With
   `kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly` (line 103), a write attempted before first device
   unlock (e.g. a background / silent-push launch after reboot) **fails**, but `DeviceIdProvider.swift:44-47`
   still caches and returns the value. The next process launch reads `nil` from the Keychain and generates a
   **different** id → the registered `deviceId` and the live `X-Device-Id` diverge and remote-revoke
   silently no-ops.

Dormant today (no screen registers the device yet); both halves confirmed **low** by both adversarial
verifiers.

## Acceptance criteria
- [ ] **AC1 — Own UUID is the persisted id.** `DeviceIdProvider` persists a freshly generated
  `UUID().uuidString` as the `X-Device-Id` value; IDFV is used (if at all) **only as an optional seed**,
  never stored verbatim as the id. A test asserts the persisted/returned id is NOT equal to the IDFV when
  an IDFV is available.
- [ ] **AC2 — Write status verified before caching.** `KeychainStore.write` surfaces the `OSStatus`;
  `DeviceIdProvider` verifies the write succeeded (`SecItemAdd`/`SecItemUpdate` == `errSecSuccess`)
  **before** caching/returning the generated id. A failed first write does NOT get cached/returned as a
  stable id (so the next launch can re-attempt rather than mint a divergent id).
- [ ] **AC3 — Stable across launches (the §2 invariant).** A test proves that across two
  `DeviceIdProvider` instances over the same Keychain the returned id is identical, and that a simulated
  write failure does not leave a cached id that would diverge on the next launch. The `X-Device-Id` the
  `HeaderAdapter` emits equals the id that would be sent as the `Device/Register` `deviceId`.
- [ ] **AC4 — Gates green.** `CleansiaCore` + both app targets compile; the Swift auth test suite is green
  (the existing TC-IOS-DEVICEID contract test plus the new cases); the blocking SwiftLint/SwiftFormat gate
  (T-0323) passes.

## Out of scope
- **No change to the no-Bearer-on-anon allow-list / `HeaderAdapter` Bearer logic** — that is the separate
  F2 / T-0332 booking checkpoint.
- **No change to `TokenStore` save/clear semantics** beyond exposing the write `OSStatus` (shared
  `KeychainStore.write` may be the same surface — keep token save behavior identical, only the status
  becomes inspectable).
- **No new device-registration screen/feature** — this hardens the existing provider only; wiring lands
  with the auth-wave home ticket.

## Implementation notes
Files: `src/cleansia_ios/CleansiaCore/Sources/CleansiaCore/Auth/DeviceIdProvider.swift:42-47` and
`KeychainTokenStore.swift:99-112` (the shared `KeychainStore.write`). Recommended: change
`KeychainStore.write` to `@discardableResult func write(...) -> OSStatus` (or return a `Bool` success), then
in `DeviceIdProvider.deviceId` mint `UUID().uuidString` (optionally seeded from IDFV per ADR if desired),
write it, and only set `cached` / return once the write status is `errSecSuccess`. Read the
`header-parity-contract.md` §2 invariant + ADR-0013 §D4.4 (the single-source `DeviceIdProvider` rule,
reviewer check #3) first. Reviewer-per-developer. No `security` gate (`security_touching: false` —
robustness/correctness on a hand-written provider, no new endpoint/authz surface), though it touches the
auth spine the reviewer must verify against checks #3 (single source) / TC-IOS-DEVICEID. No `optimizer`.

**Routing:** `[ios]` (one developer + concurrent reviewer); QA = the cross-launch stability + IDFV-seed
assertions above.

## Status log
- 2026-06-26 — draft (created by pm). Registered from `audits/AUDIT-2026-06-26-ios-phase0-foundation.md`
  F1 (adversarially-verified iOS Phase 0 foundation audit). Dedup-checked: not an existing INDEX ticket or
  prior audit finding; the affected files (`DeviceIdProvider.swift` / `KeychainTokenStore.swift`) are owned
  by the **proposed** auth-spine ticket **T-0300** (sprint-12 / Wave-10), which is this ticket's suggested
  home — `depends_on: [T-0300]` so it lands with/after the auth spine. **Deferred — not for implementation
  now** (low/latent, dormant; no shipping screen exercises the path). DoR deferred until dispatch — sized
  **S** (two small edits + tests); `layers: [ios]`; `security_touching: false`; `manual_steps: []`. No
  panel (no-decision: aligns code to the ratified §2 invariant; ADR-0013 §D4.4 in force, untouched).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->

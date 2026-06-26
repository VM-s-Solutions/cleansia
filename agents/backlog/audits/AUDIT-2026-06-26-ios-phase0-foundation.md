# iOS Phase 0 foundation audit (adversarially verified, 2026-06-26)

- **Auditor:** multi-agent analyst + reviewer panel (author + 2 adversarial verifiers), orchestrated
- **Date:** 2026-06-26
- **Scope:** the now-compiling iOS Phase 0 foundation — `src/cleansia_ios/` (the `CleansiaCore` SPM package
  + both app targets `CleansiaPartner` / `CleansiaCustomer`). Focus: the hand-written auth/session/header
  spine (`CleansiaCore/Sources/CleansiaCore/Auth/*`) against the authoritative contracts
  (`src/cleansia_ios/docs/header-parity-contract.md`, ADR-0013 §D4.4, ADR-0011 D4).
- **Method:** compared the Swift auth surface line-by-line against the header-parity contract + ADR-0013/0014
  reviewer checks #1–#13; build/tests/lint were green at audit time; each finding was attacked by two
  adversarial verifiers before being recorded.

## Summary

The iOS Phase 0 foundation builds, tests pass, and lint is clean. The **one blocker** the audit surfaced —
`API_BASE_URL` never reaching `Info.plist`, causing a launch-time `fatalError` — was **fixed and verified by
launching the app in the simulator before this audit was written**, and is NOT recorded here as open. The
remaining two findings below are **low / latent severity and fully dormant**: no shipping screen exercises
the affected code paths yet (only the auth spine itself plus its tests reference them). They are logged so
they are fixed later via the normal workflow, each folded into its natural suggested-home ticket on the
upcoming auth + booking waves — **not implemented now**. Neither blocks Phase 0 or Phase 1.

## Findings

### F1 — `DeviceIdProvider` persists IDFV as the device-id value and ignores the Keychain write status   [severity: low]   [type: bug + robustness — contract-compliance]
- **Where:** `src/cleansia_ios/CleansiaCore/Sources/CleansiaCore/Auth/DeviceIdProvider.swift:42-47`
  (the `deviceId` generation/persist path) and
  `src/cleansia_ios/CleansiaCore/Sources/CleansiaCore/Auth/KeychainTokenStore.swift:99-112`
  (`KeychainStore.write`, which discards the `SecItemUpdate`/`SecItemAdd` `OSStatus`).
- **What:**
  - **(1) IDFV persisted as the id.** First launch stores `fallbackVendorId() ?? UUID().uuidString`
    (`DeviceIdProvider.swift:42`), i.e. it persists `identifierForVendor` (IDFV) as the actual
    `X-Device-Id` value whenever IDFV is available, only falling back to a fresh `UUID` when IDFV is `nil`.
    This contradicts the project's own authoritative `docs/header-parity-contract.md` §2 (lines 95-96):
    IDFV "is not a safe substitute on its own — it can change when all vendor apps are uninstalled — so
    persist your own generated UUID and treat IDFV only as an optional seed." §2 calls this "the single
    most breakable rule on the whole auth surface" (line 58).
  - **(2) Keychain write status discarded.** `KeychainStore.write` returns `Void` and ignores the
    `OSStatus`. With `kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly` (`KeychainTokenStore.swift:103`), a
    write attempted before first device unlock (e.g. a background / silent-push launch after reboot) fails,
    but the code still caches and returns the value (`DeviceIdProvider.swift:44-47`). The next process
    launch reads `nil` from the Keychain and generates a **different** id — breaking the
    `X-Device-Id` == `Device/Register` `deviceId` string-match invariant that device-revoke depends on
    (header-parity-contract §2, lines 38-39).
- **Why it matters:** both halves break the §2 device-revoke invariant: a non-stable / non-own-UUID
  `X-Device-Id` means a remote device-revoke can silently no-op (the header id no longer matches the
  registered `deviceId`). **Latent** — dormant until the auth wave wires real screens that register the
  device; no current screen does. Confirmed low by both adversarial verifiers.
- **Proposed fix:** generate `UUID().uuidString` as the persisted id (use IDFV only as an optional *seed*
  if desired); have `KeychainStore.write` surface the `OSStatus` and verify `SecItemAdd`/`SecItemUpdate`
  succeeded **before** caching/returning, so a failed first write does not get cached as a stable id.
- **Proposed ticket:** folded into the auth-wave / partner-login suggested-home (the auth spine / login
  vertical, T-0300/T-0303) — tracked as **T-0331**. size: S  layers: [ios]

### F2 — Customer anonymous allow-list strips `Bearer` from dual-use order/payment endpoints (booking-flow design checkpoint)   [severity: low — latent design checkpoint]   [type: design / contract-parity]
- **Where:** `src/cleansia_ios/CleansiaCore/Sources/CleansiaCore/Auth/AnonymousAllowList.swift:28-39`
  (the `customerGuestBooking` list) and
  `src/cleansia_ios/CleansiaCore/Sources/CleansiaCore/Auth/HeaderAdapter.swift:29` (withholds
  `Authorization` whenever the path is on the allow-list, regardless of a present token).
- **What:** the customer allow-list deliberately adds the guest-booking surface
  (`/api/order/createorder`, `/api/order/quote`, `/api/order/lookup`, `/api/payment/createorder`,
  `/api/referral/validate`, the `*getoverview` catalogues) to the no-Bearer set. This is **intentional**
  per **ADR-0013 §D4.4** and **header-parity-contract §3** (the allow-list is the single source of
  "is this anonymous"). BUT these endpoints are **dual-use**: a signed-in customer also hits the same
  `/api/Order/CreateOrder` and `/api/Payment/CreateOrder` for in-app booking, and the backend `CreateOrder`
  reads `GetUserId() ?? string.Empty`. So a naive booking-flow port would send these calls with **no
  Bearer even when a session exists**, and the server would silently create a guest / empty-`UserId` order
  instead of associating it with the signed-in user.
- **Why it matters:** a signed-in customer's in-app booking could be silently recorded as an anonymous /
  empty-`UserId` order — the order would not associate with their account. **DISPUTED in the audit:** one
  verifier called it a real medium contract-parity issue; the other refuted it as a deliberate, documented
  design decision. Reconciled as a **design decision to honor when the booking flow is ported, not a
  current bug.** Fully latent: **no iOS feature code calls these endpoints yet** — only the allow-list and
  its test reference them.
- **Proposed fix (the decision to make at booking-port time):** send the `Bearer` when a session token
  exists (the server's `[AllowAnonymous]` still permits the genuine guest no-token case), and withhold it
  only for true guest calls. Add a test asserting `/api/Order/CreateOrder` with a stored token carries
  `Authorization` for the customer host.
- **Proposed ticket:** attach as an explicit **design checkpoint / acceptance criterion** on the customer
  booking-wizard suggested-home (T-0313), cross-referencing ADR-0013 §D4.4 and header-parity-contract §3 —
  tracked as **T-0332**. size: S  layers: [ios]

## Not-issues considered
- **`API_BASE_URL` → `Info.plist` launch `fatalError`** — was a real blocker, **already fixed + verified in
  the simulator before this audit**; NOT carried as an open finding.
- **The customer `customerGuestBooking` allow-list entries themselves** — they are correct and intentional
  per ADR-0013 §D4.4 / header-parity-contract §3 for the genuine **guest** booking path. F2 is NOT a
  request to remove them; it is the dual-use-when-signed-in checkpoint the booking-flow port must honor.

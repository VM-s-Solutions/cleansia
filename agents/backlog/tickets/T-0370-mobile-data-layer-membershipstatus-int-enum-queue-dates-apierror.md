---
id: T-0370
title: "Mobile data-layer fixes — MembershipStatus int-enum contract lie, generated-client response queue off main + parallel prefetch, offset-less date-decoder hardening, ApiError.fromGenerated → ProblemDetails code extraction (absorbs T-0367)"
status: in_progress
size: M
owner: ios
created: 2026-07-03
updated: 2026-07-03
depends_on: []
blocks: []
stories: []
adrs: [ADR-0011, ADR-0014, ADR-0019]
layers: [backend, ios, android]
security_touching: false
priority: high
manual_steps: [mobile-spec-regen (owner: scripts/refresh-mobile-spec.sh), client-regen-both (owner: scripts/generate-api-clients.sh + Android openApiGenerate)]
sprint: 12
source: phase/ios-fix1 on-device shakeout diagnosis (2026-07-02, cluster data-layer)
---

> **Dev DISPATCHED (in_progress).** Explains the owner's "retrieving information doesn't work" + "app lags"
> symptoms. None of these are iOS-16-specific — they reproduce on any version; masked in sim testing because
> fresh test users have `status: null` memberships and the sims never exercised subscribed accounts.
> **ABSORBS T-0367** (the iOS error-l10n residual — item 4 below is that exact fix; T-0367's INDEX row is
> marked absorbed).

## Context (4 findings, data-layer cluster)
1. **Membership fails to decode for ANY subscribed user — a contract lie in the committed spec.** The
   backend's `TolerantEnumConverterFactory` ALWAYS writes enums as integers
   (`Cleansia.Config/Abstractions/TolerantEnumConverterFactory.cs:81-86`), but the mobile host's
   `EnumSchemaFilter` emits STRING schemas unless the enum carries `[SwaggerEnumAsInt]`
   (`Cleansia.Web.Mobile.Customer/SwaggerSchemaFilters/EnumSchemaFilter.cs:23,37-38,52-56`) — and
   `MembershipStatus` lacks the attribute (`Cleansia.Core.Domain/Memberships/MembershipStatus.cs:10-23`). The
   generated Swift enum is String-backed (`CleansiaCustomerApi/Models/MembershipStatus.swift:13`), the wire
   value is int 1–4 → `typeMismatch` kills the ENTIRE `GetMyMembership` response
   (`GetMyMembership.cs:21`). **Android has the same latent bug**: it generated the same wrong String enum
   and `MembershipStatus` is absent from `IntEnumSerializersModule`
   (`customer-app/.../core/network/IntEnumSerializers.kt`).
2. **"App lags" — every generated-client response is processed AND JSON-decoded on the MAIN queue.**
   `CleansiaCustomerApi/APIs.swift:16` (`apiResponseQueue = .main`, never overridden) +
   `URLSessionImplementations.swift:162-163,278+`; compounded by `CustomerShellView.prefetch()` awaiting SIX
   network calls strictly sequentially (`CustomerShellView.swift:120-127`). On ADR-0014 floor hardware
   (iPhone 8/X-class) the main-thread decode bursts are visible stutter.
3. **Strict-decode landmine — offset-less date-times kill entire responses.** The generated
   `OpenISO8601DateFormatter` chain accepts ONLY date-times WITH an explicit offset
   (`OpenISO8601DateFormatter.swift:34,47-55`); executed test: `2026-07-02T10:11:12` and the 7-digit-fraction
   offset-less form parse to nil → the whole endpoint payload fails. Latent today (Npgsql timestamptz
   round-trips Kind=Utc with Z) but any `timestamp without time zone` column or Unspecified-Kind DateTime
   added to a mobile DTO fails the entire screen. (The "non-optional field" suspicion was DISPROVEN — every
   generated Swift property is Optional.)
4. **Business error codes dropped at 24 of 25 generated-client call sites — snackbars show raw
   ProblemDetails JSON** (undercuts commit `6bf55f14`). `ApiError.fromGenerated` hardcodes `code: nil` + the
   raw body as message (`CustomerGeneratedError.swift:10-11`) while the correct parser exists
   (`ProblemDetailsError.swift:12-16`); `ApiErrorLocalizer` needs `error.code`
   (`CleansiaCore/Snackbar/ApiErrorLocalizer.swift:14-24`). Grep: 24 files use `fromGenerated`, 1 uses
   `ProblemDetailsError.map`. **= T-0367, absorbed here.**

## Acceptance criteria
- [ ] **AC1 (backend attribute)** — `MembershipStatus` carries `[SwaggerEnumAsInt]`; the re-dumped mobile
  spec declares it as an integer enum (parity with every other enum). A guard/audit note is added so any
  future enum on a mobile DTO gets the attribute (test or checklist line).
- [ ] **AC2 (clients, AFTER the owner regen)** — iOS: the regenerated `MembershipStatus` is int-backed and
  `GetMyMembership` decodes for a subscribed user (non-null status). Android: `MembershipStatus` added to
  `IntEnumSerializersModule`; the membership screen loads for a subscribed user. **HOLD this half until the
  owner confirms the manual steps** — the PM holds dependent work, never runs the regen.
- [ ] **AC3 (response queue + prefetch)** — Both apps set the generated clients'
  `apiResponseQueue = DispatchQueue(label: "cz.cleansia.api.response", qos: .userInitiated)` at the container
  seam (`CustomerAppContainer.installGeneratedClientAuth()` + the partner twin) — safe because call sites
  await via continuations and ViewModels are `@MainActor`; `CustomerShellView.prefetch()` runs its six calls
  concurrently (`async let`/`withTaskGroup`). Evidence: no `CodableHelper.decode` on main in a profile trace,
  suites green.
- [ ] **AC4 (date hardening)** — `CodableHelper.jsonDecoder` gains a `dateDecodingStrategy` that falls back
  to an offset-less `yyyy-MM-dd'T'HH:mm:ss[.fff…]` parse (assume UTC) after the existing chain, in BOTH apps;
  unit test proves offset-less forms (0/3/7-digit fractions) decode and the existing Z-suffixed +
  date-only forms are unchanged.
- [ ] **AC5 (error codes)** — `ApiError.fromGenerated` delegates to the `ProblemDetailsError.map` body-parse
  so ALL 25 generated-client call sites produce code-bearing `ApiError`s (raw body only as last-resort
  message); a business error from profile/devices/disputes/addresses/loyalty/membership/booking clients
  surfaces as the LOCALIZED catalog string, not raw JSON. Closes T-0367.

## Out of scope
- Translating the 144 missing `error_*` keys — **T-0366** (separate, unchanged).
- The in-sheet snackbar occlusion (errors invisible UNDER the booking sheet) — **T-0371**.
- Any OrderStatus/spec drift work — the diagnosis verified the committed spec is NOT stale (the one defect is
  the MembershipStatus representation lie).

## Implementation notes
- Layer order per routing: the backend attribute lands first (one-line Domain change + spec filter already
  handles it) → **owner manual steps** (spec re-dump + both-client regen) → the Android serializer entry +
  the iOS int-enum verification ride the regen. Items 2–4 are NOT regen-gated — dev proceeds on them
  immediately.
- Mirror AC3 + AC4 in the PARTNER app (same generated-client defaults).
- The Android half of AC2 is a one-line module entry — same dev dispatch, android layer.

## Status log
- 2026-07-03 — filed `in_progress` by pm from the phase/ios-fix1 diagnosis (data-layer cluster); dev
  dispatched on items 2–4 + the backend attribute; the regen-gated halves of AC2 are ON HOLD pending the
  owner manual steps (flagged in the INDEX banner). T-0367 absorbed into item 4 (dedup — same fix).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

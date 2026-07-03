---
id: T-0370
title: "Mobile data-layer fixes — MembershipStatus int-enum contract lie, generated-client response queue off main + parallel prefetch, offset-less date-decoder hardening, ApiError.fromGenerated → ProblemDetails code extraction (absorbs T-0367)"
status: done
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
- [x] **AC1 (backend attribute)** — `MembershipStatus` carries `[SwaggerEnumAsInt]`; the re-dumped mobile
  spec declares it as an integer enum (parity with every other enum). A guard/audit note is added so any
  future enum on a mobile DTO gets the attribute (test or checklist line). *(Guard = `MobileSpecEnumGuardTests`,
  red→green proven; + the `patterns-mobile.md` checklist row.)*
- [x] **AC2 (clients, AFTER the owner regen)** — iOS: the regenerated `MembershipStatus` is int-backed and
  `GetMyMembership` decodes for a subscribed user (non-null status). Android: `MembershipStatus` added to
  `IntEnumSerializersModule`; the membership screen loads for a subscribed user. *(DEVIATION, recorded in the
  status log: the manual steps ran IN-BRANCH — disposable-postgres spec re-dump + Kotlin client regen,
  Android compile green; the iOS generated client is gitignored/CI-regenerated. The owner VERIFIES the
  committed spec diff at the phase PR or re-runs the pipeline; the guard test pins the representation
  either way. Subscribed-user screen verification on device = the owner device pass.)*
- [x] **AC3 (response queue + prefetch)** — Both apps set the generated clients'
  `apiResponseQueue = DispatchQueue(label: "cz.cleansia.api.response", qos: .userInitiated)` at the container
  seam (`CustomerAppContainer.installGeneratedClientAuth()` + the partner twin) — safe because call sites
  await via continuations and ViewModels are `@MainActor`; `CustomerShellView.prefetch()` runs its six calls
  concurrently (`async let`/`withTaskGroup`). Evidence: the Gate-4d install-seam tests assert the ACTUAL
  installed queue/decoder per app (red-proved on partner), suites green.
- [x] **AC4 (date hardening)** — `CodableHelper.jsonDecoder` gains a `dateDecodingStrategy` that falls back
  to an offset-less `yyyy-MM-dd'T'HH:mm:ss[.fff…]` parse (assume UTC) after the existing chain, in BOTH apps;
  unit test proves offset-less forms (0/3/7-digit fractions) decode and the existing Z-suffixed +
  date-only forms are unchanged.
- [x] **AC5 (error codes)** — `ApiError.fromGenerated` delegates to the `ProblemDetailsError.map` body-parse
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
- 2026-07-03 — all four fixes landed in `5252bfb9` (spec re-dumped locally against a disposable
  postgres — NOT the owner regen; the two ride-along drift entries AppleAuth + ConfirmUserEmail.email
  verified as stale-spec catch-up). Suites at commit time: Core 252, Customer 377, Partner 369, dotnet
  0 errors, `:customer-app:compileDebugKotlin` green.
- 2026-07-03 — review CHANGES folded (3 test/guard additions, no production-code change):
  (1) **AC1 spec-enum guard** — `Cleansia.Tests/Configuration/MobileSpecEnumGuardTests.cs` pins every
  enum-carrying `components.schemas` entry in BOTH committed mobile specs to `type: integer`
  (16 customer + 12 partner enum schemas covered). **Red→green proven**: flipping MembershipStatus
  back to `string` in the spec fails the customer case naming the offender; restored, 2/2 green.
  (2) **3-digit offset-less fraction** case added to `ApiDateDecodingTests` (Core) and
  `GeneratedDateDecodingTests` (customer) — green.
  (3) **Gate-4d install-seam tests** — `PartnerInstallSeamTests` + `CustomerInstallSeamTests`
  construct the REAL container, call `installGeneratedClientAuth()`, and assert the ACTUAL
  `CodableHelper.jsonDecoder` decodes an offset-less date and `…ApiAPI.apiResponseQueue !== .main`.
  **Red→green proven on partner**: with the two container install lines deleted, both tests fail
  (2/2 red), restored → full partner suite green. Customer red-proof is by the identical mechanism
  (nothing else in the test process sets the customer-module globals); not re-run mid-Slice-A-churn.
  Post-fold suites: Core **253**, Customer **381**, Partner **371**, MobileSpecEnumGuardTests 2/2 —
  all green on iPhone 17 sim; customer target compiled fine on the single attempt (no deferral
  needed). Global-state mutation in the seam tests follows the suite's existing practice
  (`GeneratedClientAuthAdapterTests` already installs per-test bridges).
- 2026-07-03 — **AC2 deviation (recorded):** the `manual_steps` ran IN-BRANCH rather than held for the
  owner pipeline — the mobile spec re-dumped against a disposable postgres (`5252bfb9`; the two ride-along
  drift entries — AppleAuth + `ConfirmUserEmail.email` — verified as stale-spec catch-up, not new surface)
  and the Kotlin client regenerated from the new spec (`:customer-app:compileDebugKotlin` green;
  `MembershipStatus` in `IntEnumSerializersModule`); the iOS generated client is gitignored (CI
  regenerates). OWNER: verify the committed spec diff at the phase PR (or re-run
  `scripts/refresh-mobile-spec.sh` + the regens) — `MobileSpecEnumGuardTests` pins every mobile enum
  schema to integer either way.
- 2026-07-03 — **done** by pm at phase close (reviewer PASSED on substance; the 3 CHANGES folded in
  `ebf2fcfd` with red→green proofs). Final-tree gates: **dotnet 1714/1714** (incl. the spec-enum guard);
  Core 272/272 on iPhone 17 AND iOS 16.4; Customer 406/406 + Partner green; Android compile green on the
  regenerated client; lint clean tree-wide. Closes **T-0367** (absorbed). The subscribed-user membership
  screen on a real device rides the owner device pass (phase PR).
- 2026-07-03 — fix-round 2 by ios+backend (**owner-reported on-device defect, filed here as the
  dates/decode-cluster carrier**): **customer profile save was broken end-to-end.**
  (a) iOS omitted the required `id` on `UpdateCurrentUserCommand` — the backend own-account guard
  (`user.Id == command.Id`, `UpdateCurrentUser.cs:70`) rejects every save. Fixed by Android-parity JWT
  threading (`UserRepository.kt:85-96,111`): `CurrentUserProfile` + `ProfileUpdate` gain `id`,
  `LiveUserProfileClient` (now injected with the spine `TokenStore`) pulls it from the JWT sub claim via
  `JwtDecoder.userId(of:)` at fetch time, `ProfileViewModel.save` guards a nil `currentUser` (graceful
  snackbar+`.error`, never a nil-id send). **Red→green:** `testSaveCarriesCurrentUserIdAndPickedBirthDate`
  + `testSaveWithoutLoadedUserFailsWithoutCallingUpdate` (ProfileViewModelTests) + 2 new
  `UserProfileClientMappingTests` pinning the generated command carries the id + the picked birthDate —
  Customer 408/408 on iPhone 17.
  (b) backend: the generated Swift client encodes `birthDate` (`format: date`) as a full ISO date-time
  (`"1990-05-01T00:00:00.000Z"`), which default STJ `DateOnly` handling 400s before validation. Added
  `TolerantDateOnlyConverter` (`Cleansia.Config/Abstractions`, the `TolerantEnumConverterFactory` sibling)
  registered via the new shared `CleansiaStartupBase.ConfigureJsonSerialization` (all five hosts): reads
  `"yyyy-MM-dd"` AND full ISO date-time (time truncated literally — no tz day-shift), still WRITES
  `"yyyy-MM-dd"` (Android/web wire unchanged), garbage still 400s. **Red→green:** 12 new
  `TolerantDateOnlyConverterTests` run against the REAL host serializer config (compile-red before the
  converter existed); dotnet build 0 errors; full Cleansia.Tests **1726/1726**.

## Review
- 2026-07-03 reviewer verdict (relayed via coordinator): **PASSED on substance** — all four fixes
  verified correct, the spec diff independently confirmed clean (MembershipStatus int + the two
  stale-spec catch-ups), formatter usage verified thread-safe. Three cheap CHANGES to fold, all
  test/guard additions, no production-code defects: AC1 spec-enum guard test, the 3-digit
  offset-less fraction case, and Gate-4d install-seam red tests per app. → folded, see status log.
- Harvest (rode `5252bfb9`): three rows added to `agents/knowledge/patterns-mobile.md` — the ONE
  install seam for generated-client globals (queue + hardened decoder), the ONE ProblemDetails body
  parser (`ApiError.fromProblemDetails`), and the `[SwaggerEnumAsInt]` new-mobile-enum checklist
  (now also pinned by MobileSpecEnumGuardTests).

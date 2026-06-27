# Sprint 12 — iOS PORT (Wave 10): parity Swift/SwiftUI customer + partner apps

**Status:** PHASE 0 FOUNDATION DONE + MAC-VERIFIED + MERGED (2026-06-26) · **PHASE 1 (T-0303) DONE** — proving vertical green on `phase/ios-phase1` · **PHASE 2 — T-0304 + T-0305 DONE** on `phase/ios-phase2` · **PHASE 3 — T-0306 (map seam + MapKit + partner AddressPicker) DONE + T-0310 (partner Profile + Devices + Preferences) DONE** on `phase/ios-phase3` (2026-06-27; 7 commits, pushed). **T-0306:** Slice A Core `MapProvider`/`GeocodingService` seam + `CLGeocoder` + iOS-16 `MapKitMapProvider` (125 Core tests) / Slice B partner `AddressPicker` (pan+search, returns `GeocodedAddress`); reviewer APPROVE. **T-0310:** Slice A profile hub + 6 section editors + onboarding chain + the now-live RegistrationLock Fix-CTAs (fail-closed gate #24 verified) / Slice B Devices list+revoke (**SECURITY PASS** on D6/D7/D8) / Slice C Preferences (language [+ System/follow-device row] + theme via `.preferredColorScheme`, the first runtime in-app language switch); reviewer APPROVE; 185 Partner tests. **Phase 3 PR drafted.** · Phase 2+ tail (T-0307/0308/0309/0311/0312/0313/0314 + compliance T-0324…T-0329) proposed · follow-ups filed: **T-0334** (iOS ServiceAreaRow) / **T-0335** (iOS current-location FAB, gated on owner T-0325) / **T-0336** (iOS notifications-feed spike) / **T-0337** (Android partner profile sealed-state + i18n) / **T-0338** (Core catalog i18n ×5 + swappable bundle); android i18n **T-0333** (Register/Forgot) prior · standing latent backend S8 (RefreshToken tenant read asymmetry the device-revoke kill rides on) tracked by **T-0236** (`done`) + `security/auth-sessions.md` + `security/ios-devices.md` — re-verify before any non-null-TenantId onboarding
**Created:** 2026-06-23
**Updated:** 2026-06-27
**Source:** **ADR-0013** (`adr/0013-ios-app-architecture-and-port-strategy.md`, **accepted** 2026-06-23) +
**ADR-0014** (`adr/0014-ios-deployment-target-ios16-and-state-mechanism.md`, **accepted** 2026-06-23 —
partially supersedes ADR-0013: **iOS-16 floor** + `ObservableObject`/`@Published` state + the iOS-16 MapKit
variant; all other ADR-0013 decisions stand) +
**ADR-0019** (`adr/0019-ios-generated-client-authenticates-via-the-core-spine-backed-requestbuilderfactory.md`,
**accepted** 2026-06-26 — the generated business client authenticates **only** via a Core-spine-backed
`RequestBuilderFactory`; reviewer #13-gen; surfaced by the T-0303 Understand pass). Companion living doc
`architecture/decisions/ios-app-architecture.md`. Evidence base: the **Mobile API contract audit**
(security, 2026-06-22) + the **Android parity map** (analyst, 2026-06-22). ADR-0011 D4 supplies the
born-canonical Swift `ApiResult<T>` contract.

> **Owner revision applied 2026-06-23 (Q-IOS-01 answered = iOS 16, recorded in ADR-0014):** lower the floor
> from iOS 17 to **iOS 16** for old-device reach (iPhone 8/8 Plus/X, 2017+). Real impacts: state mechanism →
> **`ObservableObject`/`@Published`** (`@Observable` is iOS-17-only; the `UiState`/`ActionState` **enums +
> facade parity are unchanged**); maps use the **iOS-16 MapKit variant** (`Map(coordinateRegion:)` +
> `MKMapView` `UIViewRepresentable` for the full-bleed/overlay surfaces — the SwiftUI `Map {...}` API is
> iOS-17-only). **No new/removed tickets, no structural change** — only the per-ticket notes + the
> deployment-target + the reviewer checks (#11/#12) below. Everything else in this plan is unchanged.

> **STATUS-LOG 2026-06-26 — PHASE 0 BUILT + MAC-VERIFIED + MERGED; CI gate landed:** the Phase-0
> foundation (authored earlier on Windows) was **compile-verified, fixed, and proven on a Mac this session**
> (Xcode 26.3, iOS-16 simulator) and is now on `master`. **CleansiaCore builds + all 68 unit tests pass on
> the iOS simulator; both app schemes (`CleansiaPartner`, `CleansiaCustomer`) build AND launch in the
> simulator; `swiftlint --strict` + `swiftformat --lint` are both clean.** A launch-crash blocker
> (`API_BASE_URL` never reaching `Info.plist` → `fatalError`) was **found, fixed, and proven by launching the
> app** (audit-verified — `audits/AUDIT-2026-06-26-ios-phase0-foundation.md`; that blocker is NOT tracked
> open). **Merged PRs:** `8220f4c` "ci: add iOS build/test/lint workflow (macOS runner) (#90)" +
> `6628172` "Fix/ios phase0 verification (#91)" (foundation commit `c1009c6`). Resulting transitions (see §3):
> **T-0296…T-0301 → `done` (verified)**; **T-0302 → wiring `done` / FIRST REAL GEN `blocked` on regen**;
> **T-0323 → `done` via CI (#90)**; **T-0303 → `blocked` on TWO owner-side items** (mobile-spec-regen +
> the dev mobile-API hosts being live — see §7.1). The Phase-0 audit logged **2 dormant deferred findings**
> (`T-0331` unblocked, next; `T-0332` booking-checkpoint) — INDEX banner. Phase 2+ stays **proposed**.

> **STATUS-LOG 2026-06-26 — PHASE 1 (T-0303) `blocked` → `done`; the proving vertical is GREEN on
> `phase/ios-phase1`.** Both owner blockers that held T-0303 (§7.1) are **CLEARED**: the dev mobile API is
> **live**, and the owner ran the **mobile-spec-regen** (the post-T-0272 specs are committed — `9232335`).
> T-0303 is implemented + reviewed + committed on `phase/ios-phase1` in **2 commits** — `8996df9` (Slice A:
> partner login spine) + `2a57f70` (Slice B: read-only Dashboard) — preceded by `d965c5b` (ADR-0019 + the
> §7.2 scope record) and `8d4cfe3` (the T-0302 codegen toolchain first real generation). **The proving
> vertical works end-to-end:** partner login (hand-written `AuthClient`, empty-token/unverified gate, router
> gates verified→dashboard vs unverified→`verifyEmail` placeholder) → authed **read-only Dashboard** (greeting
> + Weekly-earnings / Pay-period / Last-month stats cards + the 3-state hero) via the generated
> `dashboardGetStats` going out **through the ADR-0019 Core-spine-backed `RequestBuilderFactory`**. **Gates
> all green** (see §8 evidence): reviewer **#13-gen PASS** (single token source; no per-call header/token code
> outside `HeaderAdapter`; no hand-edited generated client) + **TC-IOS-GEN** passes (a generated call carries
> Bearer + `X-Device-Id`/`X-Device-Label`/`X-Time-Zone` despite the generated `requiresAuthentication:false`,
> and a 401 drives a single-flight refresh + **exactly one** retry with the rotated token); the required
> **router-gate test** (`requiresEmailConfirmation==true` → `verifyEmail`, §7.2) is present; `swiftformat
> --lint` + `swiftlint --strict` clean; **CleansiaCore 93 tests + CleansiaPartner 17 tests** pass on the
> iPhone 17 simulator; **reviewer AND security APPROVE on both slices.** Deferrals confirmed unchanged (§7.2):
> the hero's 3 states are IMPLEMENTED, live non-empty data lands in **T-0307**; greeting "jobs today" line,
> notifications bell, quick-actions grid, and silent-stale cache / pull-to-refresh defer to their named homes
> (**T-0304/0307/0310**). Two security forward-notes recorded for the later authed waves (§7.3). Resulting
> transition: **T-0303 → `done`** (§3 + INDEX Wave-10 roster). The owner commits these backlog edits to
> `phase/ios-phase1` (the PM does not commit). Phase 2+ stays **proposed**.

> **STATUS-LOG 2026-06-26 — PHASE 2 STARTED: T-0304 (partner shell + RegistrationLock + SplashGate)
> `proposed` → `done`.** Implemented + reviewed + committed on `phase/ios-phase2` in **3 commits** —
> `55b39aa` (ADR-0020 docs: the partner router pattern), `c269360` (Slice A: the fail-closed gate), `df71181`
> (Slice B: the shell). Built through the agent workflow in **two slices**, each through reviewer + the
> applicable security/Gate-DP gate; **ALL APPROVE**. **Slice A — the gate:** a `SplashGate` decision tree +
> a fail-closed `RegistrationLock`. The predicate is the **AND** of `hasCompletedProfile &&
> areDocumentsUploaded && (contractStatus == .approved(4) || .active(2))`; **any nil/unknown → LOCKED**;
> **availability is NOT a clause**. **BOTH error paths fail CLOSED:** SplashGate `.failure` → lock (never the
> shell); the lock VM's `.failure` **preserves** last-known/Missing and **never** unlocks. **Reviewer #24 +
> TC-IOS-REGLOCK green; security APPROVE** (traced the backend: `CheckCurrentEmployee` is **token-scoped +
> `[Permission]`-guarded, no client id**). The **ADR-0020 router** (reviewer #23) **reseeded `.dashboard` →
> `.splash`**, which **CLOSED a latent T-0303 fail-OPEN** the architect caught (an authed-but-incomplete
> partner previously landed straight on the authed area). The closed **14-token `missingFields` vocabulary**
> (`Employee.GetMissingProfileFields`) is **localized ×5**. **Slice B — the shell:** a native SwiftUI
> `TabView` (ADR-0018 D3) with the **4 tabs in Android `MainTab` order** (Dashboard·Orders·Invoices·Profile);
> the `.dashboard` tab hosts the **T-0303 dashboard**; the dashboard's `onOpenOrders` **switches to the Orders
> tab**; the 3 other tabs are **shared placeholders**. **Gate-DP APPROVE** (the native `TabView` divergence
> from the Android floating-island pill is the **sanctioned ADR-0018 D3 component swap**, noted). **Gates
> green** (§8): `swiftformat --lint` + `swiftlint --strict` clean; **CleansiaCore 93 + CleansiaPartner 61**
> tests pass on the iPhone 17 simulator. **§7.4 choices confirmed (developer):** (a) the Rejected-row
> contact-support affordance shipped **INERT** (no `mailto:` — the §7.4 inert option; the
> `registration_lock_action_contact_support` translation is carried for **T-0310**); (b) the lock's
> **silent-stale caching is DEFERRED** — plain load-on-appear + Retry + `.refreshable` (the §7.4-sanctioned
> option; the `STALE_WINDOW` caching lands later alongside the dashboard's deferred cache). **Deferrals homed
> (§7.4):** the lock "Fix" CTAs are inert → **T-0310** (profile-section chain); the SplashGate onboarding
> branch is deferred → **T-0305**; the pre-existing hardcoded "Verify your email — coming in T-0305"
> placeholder string (`PlaceholderVerifyEmailView`, from T-0303) localizes when **T-0305** builds the real
> ConfirmEmail screen. Resulting transition: **T-0304 → `done`** (§3 + INDEX Wave-10 roster); next runnable =
> **T-0305**. The owner commits these backlog edits to `phase/ios-phase2` (the PM does not commit). Phase 2+
> tail stays **proposed**.

> **STATUS-LOG 2026-06-26 — PHASE 2 CONT.: T-0305 (partner auth completeness — Register/Forgot/ConfirmEmail/
> Onboarding) `proposed` → `done`.** Implemented + reviewed + committed on `phase/ios-phase2` across **4
> slices** — `ccd25cd` (the §7.5 docs / Understand-pass rulings), `e232147` (Slice A: ConfirmEmail), `3e70cdb`
> (Slice B: Register + the Core `PasswordPolicy`/`PasswordRuleList`), `84d38bc` (Slices C+D: Forgot-password +
> Onboarding). Built through the agent workflow; **every slice reviewer-APPROVE**; **Slice A** (the
> auth/gate-touching one) **also security-APPROVE** (it traced the backend `ConfirmUserEmail` handler);
> **Slices C+D** got an explicit **gate-safety review (SAFE — no escalation)**. **All four flows shipped:**
> **Register** (+ the Core `PasswordPolicy`/`PasswordRuleList`, ≥8 && letter && digit — the
> `RegisterViewModel.kt:37-39` parity lifted to Core); **Forgot password** (single-phase); **ConfirmEmail**
> (replaces the `PlaceholderVerifyEmailView` placeholder; **reuses the LIVE empty-token gate** — 200+empty
> Token → `unverifiedEmail`/no app entry, 200+token → authenticated); **Onboarding** (a 2-page pre-auth intro
> + the SplashGate onboarding branch + `hasSeenOnboarding` in the new Core `AppSettingsStore`,
> UserDefaults-backed). **Security (Slice A, reviewer #25):** the spine's `send()` gained an **`httpMethod:`**
> param (`ConfirmUserEmail` is **PUT** — no silent 405); the **Bearer is withheld on the anon confirm path**
> (the **double-skip** — token present + path anon → no Authorization) and that is **SAFE** — security traced
> the backend, which resolves the user from the confirmation **CODE** alone (no session identity needed). **No
> new anon allow-list entry; Logout stays authed.** A **positive-control test** proves the double-skip
> assertion is non-tautological. **The login screen's forgot + sign-up links are both now LIVE**;
> `.verifyEmail(email:)` carries the email (**no `UserProfileStore` introduced** — the ADR-0020 fold-in). **F1
> (D5):** iOS **LOCALIZES ×5** the validation strings the Android partner Register/Forgot VMs hardcode in
> English — **iOS does it right; the Android bug is NOT replicated** — and the android fix is filed as the
> follow-up **T-0333** (independent of the iOS work). **Gates green (§8):** `swiftformat --lint` + `swiftlint
> --strict` clean; **CleansiaCore 114 + CleansiaPartner 96** tests pass on the **iPhone 17 simulator**
> (TC-IOS-CONFIRM-PUT / -SETTINGS / -PASSWORD-POLICY + the extended TC-IOS-ANON/-EMPTYTOKEN/-VERIFY-EMAIL-ARG;
> reviewer **#25 + #26 PASS**, security-APPROVE on Slice A). **Seed refinement (ADR-0020 living-doc fold-in,
> §7.5):** `PartnerRootView`'s launch seed is now **UNCONDITIONALLY `.splash`** (was
> `hasValidSession ? .splash : .login`) so the SplashGate is the **sole** launch resolver — needed for the
> onboarding-vs-login decision on un-authed first-run; the **fail-closed registration gate (#24) is
> BYTE-UNCHANGED and no bypass is introduced** (the no-session branch resolves only to
> `.unauthenticated`/`.needsOnboarding`, never `.authenticated`). **It refines, not contradicts, ADR-0020 D2**
> (one-line note added to `architecture/decisions/ios-app-architecture.md` under the ADR-0020/partner-router
> section — **no new ADR**). Resulting transition: **T-0305 → `done`** (§3 + INDEX Wave-10 roster); next
> runnable = **T-0306** (map seam + MapKit, deps T-0300✓) ∥ **T-0309** (earnings/invoices, deps T-0304✓) ∥
> **T-0310** (profile/devices, deps T-0304✓+T-0306). The owner commits these backlog edits to
> `phase/ios-phase2` (the PM does not commit). Phase 2+ tail stays **proposed**.

> **STATUS-LOG 2026-06-26 — PHASE 3: T-0310 (partner Profile tab + section editors + onboarding chain + Devices
> + Preferences) — 5 Understand-pass rulings + 2 scope items RECORDED (§7.7, no new ADR; reviewer #28; branch
> `phase/ios-phase3`).** The architect ruled D1–D5 + scope A/B (the device-id/revoke gate — decisions 6–8 — is
> ruled by the **security** charter in parallel and is OUT of this record). **All five APPLY accepted ADRs / confirm
> Android parity — NO new ADR** (ADR-0020 owns the nav trade-off; §7.5 D1 owns the settings store; §7.6 D2 owns the
> defer-the-affordance call; ADR-0018 Gate-DP owns the divergence form; the `patterns-mobile` Parity rule owns the
> E1 divergence). **D1 (nav):** the Profile tab hosts an **in-tab `NavigationStack` over a typed `ProfileRoute` enum**
> INSIDE the `.dashboard` shell — the ADR-0020 intra-audience push (the root enum stays the audience selector); the
> `ProfileRoute` shape is recorded (gate sections carry the `onboarding: Bool` payload; the AddressPicker is a modal
> return-value flow, not a route). **D2 (the load-bearing call):** the **RegistrationLock owns its OWN local
> `NavigationStack` + onboarding-chain VM and pushes the SHARED section set over ITSELF** with `onboarding == true`;
> on pop it re-resolves and **only** the success watermark flips the root to `.dashboard` — **fail-CLOSED, no
> cross-audience routing into the shell's Profile tab** (the rejected alternative breaks #24). **Section screens are
> shared as ONE set of Views/VMs hosted by TWO stacks** (Profile tab + lock), the `onboarding` flag the only switch.
> **D3:** the advisory `ServiceAreaRow` is **DEFERRED → T-0334** (a Gate-DP divergence; advisory-only, never a save
> gate); the Address section ships pan/search/save at full parity. **D4:** **EXTEND** the one `AppSettingsStore` with
> writable language + a `Theme` enum + setters; **honor theme via `.preferredColorScheme` now**. **D5:** iOS is born
> **sealed-state canonical** (`UiState<T>` load + `ActionState` save) — Android's `ProfileUiState`/`*UiState`
> **flag-bags (E1) NOT replicated**; the android E1 + string-literal fix is filed as **T-0337**. **Scope A:** the
> my-location FAB + `LocationProvider` seam are **DEFERRED → T-0335** (T-0325's `NSLocationWhenInUseUsageDescription`
> is **still `proposed`** — building the FAB now ships a dead control); the `LocationProvider` protocol shape is
> recorded regardless. **Scope B:** **"Notifications" DROPPED** from T-0310 — no Android prefs surface / no backend
> prefs API / no client; the in-app feed is a separate spike **→ T-0336** (after T-0311 APNs). Living doc
> (`architecture/decisions/ios-app-architecture.md`) + `patterns-mobile.md` updated in parallel; follow-up stubs
> **T-0334/T-0335/T-0336/T-0337** filed `draft`. T-0310 stays **proposed** (ready to build on `phase/ios-phase3`);
> the owner commits these backlog edits (the PM does not commit).

> **STATUS-LOG 2026-06-27 — PHASE 3 COMPLETE: T-0306 + T-0310 `proposed` → `done` on `phase/ios-phase3`
> (7 commits, pushed); Phase 3 PR drafted.** Both tickets passed the full workflow (ios dev → reviewer/Gate-DP,
> + security on Devices). **T-0306 (map seam + MapKit + partner AddressPicker) → `done`** —
> `480f5c4`+`03a00f3`+`199916b`: **Slice A** = the Core `MapProvider`/`GeocodingService` seam + `Coordinate`/
> `GeocodedAddress` value types + `CLGeocoderGeocodingService` + the **iOS-16 `MapKitMapProvider`** (`Map(coordinateRegion:)`
> + SwiftUI overlay pin, NO iOS-17-only `Map{Marker}` — ADR-0014 D6′; **125 CleansiaCore tests**); **Slice B** =
> the partner `AddressPickerView`/`VM` — full-bleed map + a **static center-pin** the map pans under + search +
> the **300ms forward / 500ms reverse-on-idle** debounce ported verbatim, best-effort `CLGeocoder` (cancel-before-refire,
> nil/`[]` on error, never blocks confirm), **correctly NO `UiState`/`ActionState`** (reviewer note #27 — it is an
> interactive map, not an E1/E2 screen), returns `GeocodedAddress` via `onConfirmed` (NOT wired into AddressSection —
> that is T-0310). **D2 honored:** the current-location FAB + `LocationProvider` seam are **DEFERRED → T-0335** (the
> recorded Gate-DP divergence; the picker is fully usable on the Prague default). **Reviewer APPROVE**; swiftformat/swiftlint
> clean. **T-0310 (partner Profile tab + section editors + onboarding chain + Devices + Preferences) → `done`** —
> `ce6c5fc`+`ee2f044`+`2cdaf93`+`6c6155c` across **3 slices**: **Slice A** = the profile hub (hero + contract-status
> chip + section-group rows + logout) + the **6 section editors** (Personal/Address/Identification/Bank/Emergency/Documents)
> over the `PartnerProfileClient` (ADR-0019 spine) + the **onboarding chain** + the **now-live RegistrationLock Fix-CTAs**
> (D2 — the load-bearing call: the lock owns its **OWN** `NavigationStack` + chain VM and pushes the **SHARED** section
> set over itself with `onboarding == true`; **fail-CLOSED, no cross-audience shell routing** — the §7.4 gate #24 stays
> **byte-unchanged and verified**, only the success watermark flips the root to `.dashboard`); **Slice B** = **Devices**
> (Device/Mine list + revoke) — **SECURITY PASS on all binding rules** (D6 single device-id source = the one
> `DeviceIdProvider`; D7a hide-on-current + D7b defensive self-revoke → `authClient.logout()`; D8 server-scoped revoke
> verified against the backend; **TC-IOS-DEVICES-SELF-REVOKE** green — full S1–S10 walk + the build-time verification at
> `security/ios-devices.md` 2026-06-27); **Slice C** = **Preferences** (language picker [with a **System / follow-device**
> row] + theme picker; theme honored via **`.preferredColorScheme`**; the **first runtime in-app language switch**).
> **D3** advisory `ServiceAreaRow` DEFERRED → **T-0334**; **D5** iOS born **sealed-state canonical** (`UiState<T>` +
> `ActionState`) — Android's E1 flag-bags NOT replicated → **T-0337**; the current-location FAB → **T-0335**; "Notifications"
> DROPPED (no Android prefs surface / no backend prefs API / no client) → spike **T-0336**. **Reviewer APPROVE** (including
> a re-review of the System-row fix); **185 CleansiaPartner tests** pass; swiftformat/swiftlint clean. **One reviewer MINOR
> (Slice C) filed as T-0338:** the CleansiaCore-owned user-facing strings (`ApiErrorLocalizer`'s 6 error toasts +
> `snackbar.dismiss`) ship **en-only** (Core `Package.swift defaultLocalization: en`, `bundle: .module`), so the new
> in-app language switch does **NOT** re-localize them — pre-existing debt, surfaced by T-0310, scoped to add cs/sk/uk/ru
> to the Core catalog + a swappable Core bundle. **Standing latent SECURITY item (NOT a Phase-3 regression):** the
> multi-tenant asymmetry in `RefreshTokenService.RevokeByDeviceAsync`/`GetActiveByUserIdAsync` that the remote
> device-revoke session-kill rides on (`security/auth-sessions.md`) is tracked by **T-0236** (`done` `b8f89202` — the
> read-side `IgnoreQueryFilters` fix covers `GetActiveByUserIdAsync`) + carried as the standing dependency in
> `security/ios-devices.md`; dormant in single-tenant prod, **re-verify before onboarding any non-null-`TenantId` user**.
> Resulting transitions: **T-0306 → `done`, T-0310 → `done`** (§3 + INDEX Wave-10 roster). Next runnable = **T-0307**
> (partner order work-loop, deps T-0304✓+T-0306✓) ∥ **T-0309** (earnings/invoices, deps T-0304✓). The owner commits
> these backlog edits + opens the Phase-3 PR (the PM does not commit). Phase 2+ tail stays **proposed**.
**Goal:** port the Kotlin/Compose customer + partner apps to **Swift/SwiftUI** as **parity** apps sharing
the **same Mobile API contract**, on a `CleansiaCore` SPM package + 2 app targets, **partner-first**, with
a hand-written auth/session/header layer to the exact Android contract. iOS code lives at
**`src/cleansia_ios/`** (greenfield — created on the first iOS ticket).

> Wave 8 (pre-iOS cleanup, sprint-10) deduplicated the contract surface specifically to de-risk this
> port. Wave 9 (audit log, sprint-11) is a separate backend/admin feature. **Wave 10 is the iOS port.**
> Ticket ids **T-0296…T-0314**, next free after T-0295.

---

## 1. Owner decisions this wave builds to (ADR-0013 + ADR-0014)

- **Deployment target = iOS 16** (ADR-0014 / Q-IOS-01 answered — old-device reach iPhone 8/X 2017+). Set on
  both app targets + `CleansiaCore`'s `Package.swift` (`platforms: [.iOS(.v16)]`, T-0296). State VMs use
  **`ObservableObject` + `@Published`** (not `@Observable`); maps use the **iOS-16 MapKit variant**.
- **PLAN FIRST** — this is the design (ADR-0013 + ADR-0014) + this proposed backlog; **no Swift code, no
  commits** until the owner approves.
- **THE ARCHITECT DECIDES THE LEAD APP** — decided: **PARTNER** (D9). First vertical = partner login →
  **read-only Dashboard**, proving auth/session/headers/codegen/state with **zero** Mapbox/Stripe/Google/
  photo dependencies. The shared package is **designed from the customer app's mature `:core` shape**
  (D9.1) so it isn't shaped only by the less-`:core`-mature partner code.
- **The one hard blocker = the owner mobile-spec regen** (`manual_step: mobile-spec-regen`, **owner-only**).
  The committed specs (`src/cleansia_android/openapi/{partner,customer}-mobile-api.json`, 2026-05-31) are
  pre-T-0272 (wrong login schema, no `trustedDeviceToken`, leaked `requiredProfile`/`requiredAudience`,
  **missing** `Device/Mine` + `Device/{id}` revoke + `EmployeePayroll/GetPeriodPays`). **iOS codegen MUST
  NOT run against them.** A regen of the *existing* contract (not a contract change) feeds web (NSwag),
  Android (openapi-generator kotlin), and iOS (openapi-generator swift5) from the same spec.
- **Owner questions:** **Q-IOS-01 ANSWERED — iOS 16** (ADR-0014, above). **Q-IOS-02** (no Mapbox brand
  requirement → MapKit default) + **Q-IOS-03** (omit trusted-device to match Android) remain non-blocking
  with their defaults — `questions/open.md`.
- **Maps: MapKit by default behind a `MapProvider` protocol** (D6). **Stripe: `stripe-ios` PaymentSheet**
  (D7). **Push: APNs → existing `/api/Device/*`, `Platform="ios"`** (D8). **i18n: 5 locales via String
  Catalog** (D11). **trusted-device: omit v1** (D10).

---

## 2. Phase structure (the sequence)

```
PHASE 0  FOUNDATION (blocks everything) ── runnable on approval EXCEPT the codegen first-run (held on regen)
   workspace + CleansiaCore skeleton + design tokens + DI composition root + snackbar/error center
   + the hand-written auth/session/header middleware + the Swift codegen toolchain
        │
PHASE 1  LEAD VERTICAL (partner) ── proves the architecture end-to-end ── HELD on regen (needs the client)
   partner login (hand-written auth) → read-only Dashboard (generated client + UiState)
        │
PHASE 2+ PARITY FEATURE WAVES ── ordered by complexity; the 3 hard areas called out
   partner order work-loop+photos+map  ·  customer booking wizard+Stripe  ·  Mapbox/MapKit both apps  · …
```

**Runnable-on-approval vs owner-blocked (the clean split):**
- **Runnable as soon as the plan is approved (no generated client needed):** T-0296 (workspace+package
  skeleton), T-0297 (design tokens + components), T-0298 (DI composition root), T-0299 (snackbar/error
  center), T-0300 (the hand-written auth/session/header **middleware** — it is hand-written, not
  generated), T-0301 (the header-parity **spec document**). These build against `URLSession` + `CleansiaCore`
  with **no** generated business client.
- **BLOCKED on the owner mobile-spec regen** (`manual_step: mobile-spec-regen`): T-0302 (the codegen
  toolchain **first real generation**) and **every Phase-1/2+ ticket that touches a generated client**
  (T-0303 onward). The toolchain *wiring* (T-0302) can be authored against the stale spec to prove the
  pipeline, but is **held from `done`** until the regen lands and it generates the real surface.

---

## 3. Wave-10 ticket table

| ID | Title | Size | Status | Layers | depends_on | manual_step | Phase / batch |
|----|-------|------|--------|--------|-----------|-------------|---------------|
| **T-0296** | Xcode workspace + `CleansiaCore` SPM package skeleton + 2 app targets (`CleansiaPartner`/`CleansiaCustomer`), bundle ids, signing placeholders. **Deployment target = iOS 16** on both targets + `Package.swift` `platforms: [.iOS(.v16)]` (ADR-0014) | M | **done ✅ (verified)** `c1009c6` | ios | — | — | **0 FIRST/ALONE** |
| **T-0297** | Design tokens (colors/spacing/shape/type) + the `Cleansia*` SwiftUI component parity (Button/TextField/Dropdown/Dialog/Checkbox/CodeInput) in `CleansiaCore`. **VM pattern = `ObservableObject`/`@Published`** (iOS-16, not `@Observable`); sealed `UiState`/`ActionState` enums unchanged (ADR-0014 D2′) | M | **done ✅ (verified)** `c1009c6` | ios | T-0296✓ | — | 0 |
| **T-0298** | DI composition root (`AppContainer` per app, initializer injection; the lazy no-auth refresh-session boundary) | S | **done ✅ (verified)** `c1009c6` | ios | T-0296✓ | — | 0 |
| **T-0299** | Global snackbar bus + error center (`SnackbarController` parity + the app-local `ApiError→String` localizer seam) | S | **done ✅ (verified)** `c1009c6` | ios | T-0296✓ | — | 0 |
| **T-0300** | **The auth/session/header middleware (hand-written, load-bearing)** — Keychain `TokenStore`, hand-written `AuthClient` + no-auth refresh session, `actor SessionRefresher` single-flight 401-refresh, `DeviceIdProvider` (one source), `HeaderAdapter` (X-Device-Id/Label/Time-Zone + no-Bearer-on-anon allow-list), `SessionManager`/ForcedSignOut + session-scoped cache registry | **L → split** | **done ✅ (verified)** `c1009c6` — 68 CleansiaCore tests green; **2 dormant audit findings → T-0331/T-0332** | ios | T-0296✓, T-0298✓ | — | 0 (the spine) |
| **T-0301** | **Header-parity spec document** — the invisible out-of-band contract written down for the iOS dev (X-Device-Id==Device/Register id invariant, the full anon allow-list incl. customer host, X-Time-Zone, replace-refresh-on-refresh, empty-token gate) | S | **done ✅ (verified)** `c1009c6` (`src/cleansia_ios/docs/header-parity-contract.md`) | ios, docs | — | — | 0 (no-decision doc) |
| **T-0302** | Swift codegen toolchain — openapi-generator **swift5 + urlsession**, wired into the build (script/SPM plugin, the `dependsOn(openApiGenerate)` parity), reading the **shared** mobile spec; never-hand-edit discipline | M | **WIRING done ✅ (verified)** `c1009c6` — `generate-api-clients.sh` runs Homebrew `openapi-generator`; generated 159 Swift files from the committed spec as a toolchain check, throwaway output removed. **FIRST REAL GEN `blocked` on mobile-spec-regen** | ios | T-0296✓ | **mobile-spec-regen (owner)** | 0 → first real gen **BLOCKED on regen** |
| **T-0303** | **Phase-1 partner lead vertical** — partner login (hand-written auth, empty-token gate) → **read-only Dashboard** (`dashboardGetStats` via the **ADR-0019 Core-spine-backed `RequestBuilderFactory`** + `UiState`), proving auth/session/headers/codegen/state end-to-end. **Acceptance scope fixed in §7.2** (greeting + stats-driven cards + 3-state hero + inert nav closures; caching / pull-to-refresh / notifications / live order feeds DEFERRED to T-0304/0307/0310) | M | **done ✅** `8996df9`+`2a57f70` (`phase/ios-phase1`; both §7.1 blockers CLEARED — dev API live + regen `9232335`; #13-gen + TC-IOS-GEN green; CleansiaCore 93 + CleansiaPartner 17 pass; reviewer **AND** security APPROVE both slices — §7.3 fwd-notes) | ios | T-0300✓, T-0302✓ (first real gen via `8d4cfe3`) | rides T-0302 regen + dev-API-live ✓ | **1 (the proving vertical)** |
| **T-0304** | Partner shell (Dashboard·Orders·Invoices·Profile tabs) + RegistrationLock gate (fails CLOSED) + SplashGate. **Acceptance scope + the 3 Understand-pass rulings fixed in §7.4**: Decision 1 (fail-closed gate placement + AND predicate + both error paths CLOSED — confirms the Android gate, reviewer #24 + **TC-IOS-REGLOCK**), Decision 2 (the flat-enum `PartnerRootView` router gated by `.splash` — **ADR-0020**, reviewer #23), Decision 3 (the deferral map — "Fix" CTAs + onboarding branch INERT/deferred to T-0305/T-0310, the §7.2 inert-nav precedent) | M | **done ✅** `55b39aa`+`c269360`+`df71181` (`phase/ios-phase2`; Slice A gate: AND predicate, any nil→LOCKED, availability not a clause, BOTH error paths fail closed — reviewer #24 + **TC-IOS-REGLOCK** green, **security APPROVE**; ADR-0020 router #23 reseeded `.dashboard`→`.splash`, closing a latent T-0303 fail-OPEN; 14-token `missingFields` localized ×5. Slice B shell: native SwiftUI `TabView`, 4 tabs in Android `MainTab` order, dashboard tab hosts T-0303, `onOpenOrders`→Orders tab, 3 placeholders — **Gate-DP APPROVE** (D3 component swap noted). swiftformat/swiftlint clean; **CleansiaCore 93 + CleansiaPartner 61** pass on iPhone 17 sim. §7.4 (a) contact-support INERT, (b) silent-stale cache DEFERRED. Deferrals: Fix CTAs→T-0310, onboarding branch→T-0305) | ios | T-0303✓ | — | 2 (partner) |
| **T-0305** | Partner auth completeness — Register/Forgot/ConfirmEmail/Onboarding chain. **Acceptance scope + the 5 Understand-pass rulings fixed in §7.5**: D1 (a GENERAL `AppSettingsStore` in Core, UserDefaults-backed — onboarding-seen + the 5-locale language tag, `AppSettingsRepository.kt` parity), D2 (ConfirmEmail email via the `.verifyEmail(email:)` Route associated value, NOT a `UserProfileStore` — ADR-0020 fold-in), D3 (SECURITY: no new anon entry, Logout authed, `ConfirmUserEmail` is **PUT** + the spine `send()` gains an `httpMethod:` param, the double-skip + empty-token-gate reuse — reviewer #25), D4 (a Core `PasswordPolicy` + `PasswordRuleList`, ≥8&&letter&&digit — `RegisterViewModel.kt:37-39` parity), D5 (the F1 deviation: Android partner Register/Forgot VMs hardcode English validation strings — iOS localizes ×5; android fix is a follow-up). Reviewer #25/#26 + TC-IOS-CONFIRM-PUT / -SETTINGS / -PASSWORD-POLICY / -VERIFY-EMAIL-ARG + the extended TC-IOS-ANON/-EMPTYTOKEN | M | **done ✅** `ccd25cd`+`e232147`+`3e70cdb`+`84d38bc` (`phase/ios-phase2`; 4 slices — §7.5 docs / Slice A ConfirmEmail / Slice B Register / Slices C+D Forgot+Onboarding; every slice reviewer-APPROVE, Slice A also security-APPROVE — traced the backend `ConfirmUserEmail` (CODE-resolved, no session needed → the anon double-skip is SAFE), Slices C+D gate-safety SAFE. All 4 flows shipped: Register + Core `PasswordPolicy`/`PasswordRuleList`; Forgot single-phase; ConfirmEmail replaces the placeholder + reuses the LIVE empty-token gate; Onboarding 2-page intro + SplashGate branch + `hasSeenOnboarding` in the new Core `AppSettingsStore`. #25: `send()` gained `httpMethod:` (ConfirmUserEmail PUT, no silent 405); no new anon entry, Logout authed; positive-control proves the double-skip non-tautological. `.verifyEmail(email:)` carries the email (no `UserProfileStore`). F1: iOS localizes the validation strings ×5, the Android bug NOT replicated → android follow-up **T-0333**. Seed now UNCONDITIONALLY `.splash` (ADR-0020 living-doc fold-in — refines D2; gate #24 byte-unchanged, no bypass). swiftformat/swiftlint clean; **CleansiaCore 114 + CleansiaPartner 96** pass on iPhone 17 sim) | ios | T-0303✓, T-0304✓ | — | 2 (partner) |
| **T-0306** | **Map seam + MapKit default** — `MapProvider`/`GeocodingService` protocol + `Coordinate`/`GeocodedAddress` value types in `CleansiaCore` + `MapKitMapProvider`/`CLGeocoderGeocodingService` + the partner `AddressPickerView`/`VM` (first map surface; returns `GeocodedAddress` via callback — NOT wired into AddressSection, that's T-0310). **Acceptance scope + the 4 Understand-pass rulings fixed in §7.6**: D1 (a MINIMAL `MapProvider` picker factory now; T-0307's full-bleed/overlay surface added ADDITIVELY later — "the one way iOS does maps behind the seam"; `GeocodingService` = 1:1 `ReverseGeocodingService.kt` port minus the Mapbox token/network args), D2 (current-location/my-location FAB/`LocationProvider` seam DEFERRED → T-0310 gated on T-0325's `NSLocationWhenInUseUsageDescription`; ship pan+search parity on the Prague default — the recorded **Gate-DP divergence**, architect sign-off), D3 (geocoding best-effort: cancel-before-refire, nil/`[]` on error, never block the confirm; 300ms fwd / 500ms reverse debounce VERBATIM; the picker has **NO `UiState`/`ActionState`** — reviewer note #27), D4 (NO Mapbox token / map SDK / `Package.swift` change — net secret-surface reduction; Q-IOS-02 stays "No", `MapStyles.kt` NOT ported). **iOS-16 variant (ADR-0014 D6′):** `Map(coordinateRegion:annotationItems:[])` + SwiftUI overlay pin for the picker; `MKMapView`/`UIViewRepresentable` for the (additive, T-0307) full-bleed map + polygon overlays — NO iOS-17-only `Map {...}`/`Marker`/`MapPolygon`/`onMapCameraChange`. Reviewer #7/#12/#27 + Gate-DP. **Android parity: `AddressPickerScreen.kt` + `AddressPickerViewModel.kt` + `core/location/{ReverseGeocodingService,GeocodedAddress,LocationService,MapStyles}.kt`** | M | **done ✅** `480f5c4`+`03a00f3`+`199916b` (`phase/ios-phase3`; Slice A = Core `MapProvider`/`GeocodingService` seam + `Coordinate`/`GeocodedAddress` value types + `CLGeocoderGeocodingService` + the iOS-16 `MapKitMapProvider` — **125 CleansiaCore tests**; Slice B = the partner `AddressPickerView`/`VM`: pan + search, full-bleed map + static center-pin overlay, 300ms fwd / 500ms reverse-on-idle debounce verbatim, best-effort geocode, NO `UiState`/`ActionState` (reviewer #27), returns `GeocodedAddress` via `onConfirmed`. D2 current-location FAB DEFERRED → T-0335 (the recorded Gate-DP divergence). Reviewer **APPROVE**; swiftformat/swiftlint clean) | ios | T-0300✓ | — | 2 (**HARD AREA #2 — first half**) |
| **T-0307** | **Partner order work-loop** — OrdersList + OrderDetail (full-bleed map + 3-snap sheet) + the **OnTheWay** lifecycle (Take→NotifyOnTheWay→Start→Complete) + checklist/notes/issues/timeline. **Acceptance scope + the 5 Understand-pass rulings fixed in §7.9 (architect) + the SECURITY order-action gate in §7.8:** (a) the additive `MapProvider.fullBleedMap(coordinate:)` — single pin, camera-padded, NO polygon param (no polygon data in spec; §7.6 D1 additive seam); (b) the **non-modal `SnapSheet` 16.0-floor sheet = ADR-0021** (custom `GeometryReader`+drag container, NOT a modal `.sheet`; the floor stays 16.0); (c) the pure shared `OrderPrimaryAction.action(for:isMine:hasAfterPhotos:)` machine (3 call sites, NOT inline switches; ownership trust = §7.8); (d) the T-0308 photo precursor seam (disabled/placeholder Photos slot + `hasAfterPhotos` consumer; capture additive in T-0308); (e) sealed per-pane `UiState<[OrderListItem]>` + a `RefreshPhase` enum + **PORTED** per-pane staleness cache (Android E1 flag-bag NOT replicated→T-0337; SlideToCommit→native confirm Gate-DP swap). + the Code→OrderStatus one-mapper convention. Reviewer #29/#30/#31 + Gate-DP + TC-IOS-SNAP/-ORDER-ACTION; **SECURITY gate** (§7.8, O1–O4 + the backend GetPaged read-scoping fix). **Android parity: `partner-app/.../features/orders/{OrdersListScreen,OrdersListViewModel,OrderDetailScreen,OrderDetailViewModel,OrderPrimaryAction,CleaningChecklist,StatusTimeline,OrderStatusPill,PhotosSection}.kt` + `data/orders/OrdersRepository.kt`** | **L → split** | **proposed** | ios | T-0304✓, T-0306✓ | — | 4 (**HARD AREA #3**; `phase/ios-phase4`) |
| **T-0308** | **Partner photo upload** — camera capture → **JSON base64** photos (partner shape) on OrderDetail | M | **proposed** | ios | T-0307 | — | 2 (HARD AREA #3 cont.) |
| **T-0309** | Partner earnings + invoices + PeriodPay (`EmployeePayroll/GetPeriodPays` — a regen'd-spec endpoint) | M | **proposed** | ios | T-0304 | — | 2 (partner) |
| **T-0310** | Partner **Profile tab** (replaces `PartnerShellView.swift:36` `PlaceholderTab`) — the hub (hero + contract-status chip + section-group rows + logout) + **6 section editors** (Personal/Address/Identification/Bank/Emergency/Documents) over a new `PartnerProfileClient` (ADR-0019 spine) + the **onboarding chain** + **Devices** (Device/Mine list + revoke — **SECURITY-ruled, decisions 6–8**) + **Preferences** (Language/Theme). **Acceptance scope + the 5 Understand-pass rulings fixed in §7.7:** D1 (in-tab `NavigationStack` over a typed `ProfileRoute` enum — ADR-0020 intra-audience push, reviewer #28a), D2 (**the load-bearing call** — the RegistrationLock owns its OWN local `NavigationStack` + chain VM and pushes the SHARED section set over itself with `onboarding == true`; fail-closed, no cross-audience shell routing — reviewer #28b + TC-IOS-LOCK-CHAIN, composes with #24), D3 (`ServiceAreaRow` DEFERRED → T-0334, a Gate-DP divergence; Address ships pan/search/save at parity), D4 (EXTEND the one `AppSettingsStore` with writable language + a Theme enum + setters; honor theme via `.preferredColorScheme` now — reviewer #28c), D5 (born sealed-state canonical — Android E1 flag-bags NOT replicated; android fix → T-0337; reviewer #28d). **Scope cuts (PM to record):** current-location FAB + `LocationProvider` seam DEFERRED → T-0335 (gated on T-0325); **"Notifications" DROPPED** (no Android prefs surface / no backend prefs API / no client; the in-app feed is a separate spike → T-0336). Reviewer #28 + TC-IOS-PROFILE-ROUTE/-LOCK-CHAIN/-SECTION-SHARED/-SETTINGS-THEME/-PROFILE-STATE. Gate-DP. **Android parity: `partner-app/.../features/profile/` (`ProfileScreen.kt`/`ProfileViewModel.kt`, the `*Section*` set, `OnboardingChainHeader.kt`, `SectionScaffold.kt`, `AddressSectionScreen.kt`) + `features/orders/{RegistrationLockViewModel,OnboardingChainViewModel}.kt` + `core/settings/AppSettingsRepository.kt`** | M | **done ✅** `ce6c5fc`+`ee2f044`+`2cdaf93`+`6c6155c` (`phase/ios-phase3`; 3 slices. Slice A = the profile hub + 6 section editors (Personal/Address/Identification/Bank/Emergency/Documents) + onboarding chain + the now-live RegistrationLock Fix-CTAs (D2: the lock owns its OWN `NavigationStack`+chain VM, pushes the SHARED section set with `onboarding==true`, fail-CLOSED — gate #24 byte-unchanged, verified). Slice B = Devices (Device/Mine list + revoke) — **SECURITY PASS** on all binding rules (D6 single device-id source, D7a hide-on-current + D7b defensive self-revoke sign-out, D8 server-scoped revoke verified vs the backend; TC-IOS-DEVICES-SELF-REVOKE green). Slice C = Preferences (language [+ a System/follow-device row] + theme pickers; theme honored via `.preferredColorScheme`; the first runtime in-app language switch). D3 `ServiceAreaRow` DEFERRED → T-0334; D5 born sealed-state, Android E1 flag-bags NOT replicated → T-0337; current-location FAB → T-0335; Notifications DROPPED → T-0336. Reviewer **APPROVE** (incl. a re-review of the System-row fix); **185 CleansiaPartner tests**; swiftformat/swiftlint clean) | ios | T-0304✓, T-0306✓ | — | 2 (partner) |
| **T-0311** | **Push (APNs)** — register for remote notifications → APNs token + `Platform="ios"` + same `X-Device-Id` to `/api/Device/*`; re-register on login, clear on logout (the `:core` push parity) | M | **proposed** | ios | T-0300 | **owner: APNs auth key/cert** | 2 (cross-app) |
| **T-0312** | **Customer app shell + auth** — SignIn/SignUp/EmailVerify (+ Google Sign-In, customer-only) + Main shell (Home·Orders·Rewards·Profile + center **Book FAB**) | M | **proposed** | ios | T-0302, T-0306 | rides regen | 2 (customer) |
| **T-0313** | **Customer booking wizard + Stripe** — the 3-step Bolt-style anchored sheet (Services / WhenWhere / Confirm), client-side pricing, **cash→success vs card→`stripe-ios` PaymentSheet** | **L → split** | **proposed** | ios | T-0312 | — | 2 (**HARD AREA #1 — hardest**) |
| **T-0314** | Customer parity tail — Home, Orders+OrderDetail, Rewards/loyalty, Membership (SubscribePlus→Stripe), Recurring, **Disputes (multipart `IFormFile` evidence)**, Addresses (map seam), Profile/Settings (incl. **DeleteAccount/GDPR**, Devices, Notification prefs) | **L → split** | **proposed** | ios | T-0312, T-0306, T-0313 | — | 2 (customer) |

**Sizing note:** the three `L → split` tickets (T-0300, T-0307, T-0313, T-0314) are the effort
concentrators and **must be split into child tickets by the PM before dispatch** (the catalog bans `L`
in-flight). Indicative splits are in §6. Every ticket carries **reviewer-per-developer**; the auth spine
(T-0300) and the booking+payment + GDPR-delete surfaces (T-0313, T-0314) carry a **security gate**.

**Design-parity note (Gate-DP, ADR-0018) — binds the SCREEN tickets:** the first vertical **T-0303** and
**every** feature-wave screen ticket (**T-0304, T-0305, T-0306, T-0307, T-0308, T-0309, T-0310, T-0312,
T-0313, T-0314**) must satisfy **Gate-DP** — the screen's **layout/flow/branding match the cited Android
Compose screen**, built with **native SwiftUI components** (no Material re-impl), and any Android↔iOS
**conflict resolved iOS-native + noted** (component only, never layout/flow). Each such ticket **cites its
Android Compose counterpart**. The pure-infra tickets (T-0296/0298/0300/0301/0302/0311/0323) are **N/A**.
See §10.6 + §G of `ios-app-review-checklist.md`.

---

## 4. The three effort-dominating areas (called out + sized)

| Hard area | Tickets | Why it dominates | Sequencing safeguard |
|---|---|---|---|
| **#1 Customer booking wizard + Stripe PaymentSheet** | **T-0313** (L→split) | The Bolt-style 3-step anchored sheet + client-side pricing + cash/card branch + SCA/3DS via PaymentSheet — the customer **primary** flow AND its hardest feature | Built **last**, on a foundation already proven by the partner vertical (the reason for partner-first, D9) |
| **#2 Mapbox vs MapKit across BOTH apps** | **T-0306** (seam+MapKit+partner picker), reused by **T-0307/T-0310/T-0314** | Partner OrderDetail full-bleed map + 3-snap sheet + both address pickers; the single biggest cross-app vendor decision | Decided MapKit-default-behind-`MapProvider` (D6); first exercised on the partner picker (T-0306) so the choice is proven before the customer picker depends on it |
| **#3 Partner order work-loop + photo upload + the codegen toolchain** | **T-0302** (toolchain), **T-0307** (work-loop, L→split), **T-0308** (photos) | Foundational toolchain blocks all verticals; the work-loop is the richest partner feature (OnTheWay lifecycle, map, sheet, photos) | Toolchain is Phase 0; the work-loop is the **first** Phase-2 feature so the order seam is proven before customer commerce |

---

## 5. Dependency-ordered batch plan

```
PHASE 0 (runnable on approval — the foundation)
  T-0296 (workspace+package) ── FIRST/ALONE ──┐
        ├─► T-0297 (tokens+components)         │
        ├─► T-0298 (DI root) ─► T-0300 (AUTH SPINE, L→split) ─► T-0306 (map seam+MapKit)
        ├─► T-0299 (snackbar/error)            │
        ├─► T-0301 (header-parity spec)  [no deps]
        └─► T-0302 (codegen toolchain) ── wiring runnable; FIRST REAL GEN held on regen ──┐
                                                                                          │
        ── OWNER: mobile-spec-regen (the one hard blocker) ───────────────────────────────┤
                                                                                          │
PHASE 1 (held on regen)                                                                   ▼
  T-0303 (partner login → read-only Dashboard) ◀── needs T-0300 (auth) + T-0302 (client)

PHASE 2+ (after Phase 1 proves the architecture)
  partner:  T-0304 (shell) ─► {T-0305 auth-rest, T-0307 order-loop ─► T-0308 photos, T-0309 pay, T-0310 profile/devices}
  map:      T-0306 (seam) reused by T-0307 / T-0310 / T-0314
  push:     T-0311 (APNs) ── after T-0300 ── cross-app
  customer: T-0312 (shell+auth) ─► T-0313 (booking+Stripe, HARDEST) ─► T-0314 (customer tail)
```

**Dispatch order:**
1. **On approval:** T-0296 first/alone → then fan out {T-0297, T-0298, T-0299, T-0301} + start T-0300
   (auth spine, split) + author T-0302 wiring.
2. **OWNER regen** (the one blocker) → releases T-0302's first real generation → **T-0303** (Phase 1).
3. **After Phase 1 proves the architecture:** partner Phase-2 batch, the map seam (T-0306), push (T-0311),
   then the customer batch ending in the booking wizard (T-0313) + the tail (T-0314).

---

## 6. Indicative splits for the `L` tickets (PM finalizes before dispatch)

- **T-0300 (auth spine) →** (a) Keychain `TokenStore` + `DeviceIdProvider` single-source; (b) hand-written
  `AuthClient` + no-auth refresh session + empty-token gate; (c) `actor SessionRefresher` single-flight +
  ForcedSignOut + session-scoped-cache registry; (d) `HeaderAdapter` (X-Device-Id/Label/Time-Zone + anon
  allow-list). Each child red-first against the TC-IOS-AUTH-401 / ANON / DEVICEID / EMPTYTOKEN contract.
- **T-0307 (partner order-loop) →** (the §7.9 slices) **A** the additive Core `fullBleedMap(coordinate:)`
  `MapProvider` method (single pin); **B** OrdersList (3 panes, sealed per-pane `UiState`+`RefreshPhase`, ported
  staleness cache, native inline-confirm); **C** OrderDetail shell (the `fullBleedMap` + the non-modal `SnapSheet`
  3-snap container — **ADR-0021**); **D** the OnTheWay lifecycle + the pure `OrderPrimaryAction.action(…)` machine;
  **E** checklist/notes/issues/timeline + the T-0308 photo-precursor seam (disabled Photos slot). Each slice
  red-first; D carries the **SECURITY** order-action gate (§7.8 O1–O4).
- **T-0313 (booking+Stripe) →** (a) the anchored 3-step sheet scaffold + step nav; (b) Services step +
  client-side pricing; (c) WhenWhere step (map seam); (d) Confirm + cash→success vs card→PaymentSheet.
- **T-0314 (customer tail) →** split by feature cluster (Home/Orders · Rewards/Membership/Recurring ·
  Disputes-multipart · Addresses · Profile/Settings/GDPR-delete/Devices).

---

## 7. Owner manual-steps & provisioning (NOT the agents)

1. **mobile-spec-regen (owner-only) — the one hard blocker.** Regenerate **both** mobile specs
   (`src/cleansia_android/openapi/{partner,customer}-mobile-api.json`) to the current post-T-0272 contract
   (correct `MobileLogin`/`MobilePartnerLogin` schema with optional `trustedDeviceToken`, `[JsonIgnore]`d
   refresh fields, **plus** `Device/Mine` + `Device/{id}` revoke + `EmployeePayroll/GetPeriodPays`). This
   is a regen of the *existing* contract — no backend code change. It also re-feeds Android's
   openapi-generator (kotlin) from the same spec, keeping all three clients aligned. **Gates T-0302's first
   real generation and every generated-client ticket (T-0303+).**
2. **APNs auth key / push certificate (Apple Developer)** — for T-0311 push (the Android
   `google-services.json` analogue). Owner provisioning; flagged, not built by agents.
3. **Apple Developer account + signing / bundle ids** (`cz.cleansia.partner` / `cz.cleansia.customer`) —
   owner setup for T-0296's targets.
4. **Mapbox token — ONLY IF** Q-IOS-02 flips the default to Mapbox (D6 fallback). **Not needed** under the
   MapKit default; no extra owner ops burden by default.

**No ef-migration, no backend code change** in this wave — iOS is a client of the existing contract; push
is already iOS-ready. The only owner contract step is the **regen of the existing spec**.

### 7.1 T-0303 (Phase-1 partner vertical) — the TWO owner-side blockers (recorded 2026-06-26; **BOTH CLEARED 2026-06-26**)

> **CLEARED 2026-06-26 — T-0303 is now `done` (§3 / status-log).** Both owner-side items below have been
> resolved: **(1)** the owner ran the **mobile-spec-regen** — the post-T-0272 specs are regenerated and
> committed (`9232335`), so the T-0302 first real generation ran against the current contract (`8d4cfe3`);
> **(2)** the **dev mobile-API hosts are live** — the Phase-1 vertical proved auth/session/headers end-to-end
> against the live dev API. The historical record of the two blockers is kept below for traceability.

**T-0303 and every generated-client ticket (T-0303 onward) were `blocked` until BOTH cleared (now both clear):**

1. **mobile-spec-regen (owner-only) — ✅ DONE (`9232335`).** The committed specs
   `src/cleansia_android/openapi/{partner,customer}-mobile-api.json` are **stale** — last touched
   **2026-05-31** (commit `1d15484`), **pre-T-0272**. **iOS codegen MUST NOT run against them.** The
   T-0302 wiring is proven (it generated 159 Swift files from the committed spec as a toolchain check,
   then the throwaway output was removed) — but the *first real generation* waits on this regen so the iOS
   client is built from the current post-T-0272 contract (same regen also re-feeds web NSwag + Android
   openapi-generator).
2. **Dev mobile-API hosts unreachable — ✅ NOW LIVE.** On **2026-06-26** an earlier `curl` to
   `https://api-cleansia-partner-mobile-weu-dev.azurewebsites.net` **and** the `-customer-` host both
   returned **HTTP 000 (not live)**. The Phase-1 vertical proves auth/session/headers end-to-end against a
   live dev API; it could not run until the owner brought the dev mobile API up (Wave-11 provisioning —
   T-0317/T-0318/T-0320, `status/sprint-13.md` §7). The owner has since brought the dev mobile API up, and
   the Phase-1 vertical proved the spine against it (T-0303 `done`).

Both items are now cleared — **T-0303 is `done`** (the `8996df9`+`2a57f70` proving vertical on
`phase/ios-phase1`), and the generated-client seam (ADR-0019) is proven for the later authed waves to copy.

### 7.2 T-0303 — acceptance scope of the read-only Dashboard proving vertical (recorded 2026-06-26, architect)

> **CONFIRMED-AS-SHIPPED 2026-06-26 (reviewer ask).** What shipped in T-0303 matches this scope record
> exactly. **Hero phrasing confirmed unambiguous:** the **3 hero states are IMPLEMENTED** (next-job /
> available-work / empty), driven by `dashboardGetStats`-derivable data; the **live non-empty data lands in
> T-0307** (the partner order work-loop, where OrdersList + the upcoming/available-jobs feeds exist). The IN
> and DEFERRED bullets below were tightened so "implemented states vs deferred live data" reads cleanly. All
> other deferrals below are **confirmed deferrals, not cuts** — each to its named home (T-0304/0307/0310).

T-0303 is the **proving vertical** (ADR-0013 D9): its job is to prove **auth/session/headers/codegen/state
end-to-end**, *not* to reach Android dashboard parity. The full Android partner dashboard
(`partner-app/.../features/dashboard/DashboardScreen.kt` + `DashboardViewModel.kt` + `DashboardRepository.kt`)
is **3 endpoints** (`dashboardGetStats` + upcoming-orders + available-jobs-preview), a **singleton repo with
silent-stale 60s caching + a dedup mutex**, **pull-to-refresh** (the suds indicator, bound to user pulls only),
an **unread-notifications DB feed** (the bell badge), and **6 navigating cards/tiles**. Pulling all of that into
the proving vertical would drag in caching, a notifications DB, and 6 navigation targets that don't yet exist —
none of which proves anything about the load-bearing spine. **Scoped accordingly (architect ruling — confirms
the Understand brief):**

**IN — T-0303 acceptance scope (the minimum that proves the spine + codegen + state, end-to-end):**
- **Partner login** via the hand-written `AuthClient` (the empty-token unconfirmed-email gate, ADR-0013 D4.2)
  → a session in the Keychain `TokenStore`.
- **The router MUST honor `requiresEmailConfirmation` (REQUIRED — a security gate, not cosmetic).** On a
  successful token-bearing login, the router branches on `LoginSuccess.requiresEmailConfirmation`:
  `== true` → a minimal **`verifyEmail` placeholder** destination (honest stub; the real ConfirmEmail flow is
  **T-0305**), `else` → dashboard. A **token-bearing UNVERIFIED partner must NOT land on the authed dashboard** —
  this is the router-level sibling of the empty-token/confirm gate (ADR-0013 D4.2). **Required acceptance
  evidence:** a router/VM-level routing test asserting `requiresEmailConfirmation == true` routes to
  `verifyEmail` (the sibling of **TC-IOS-EMPTYTOKEN**, §8) — this is a **required** test, not "if practical".
  Session presence on the root view gates via the **read-only `hasValidSession`** accessor (the concrete
  `TokenStore`/`save`/`clear` stay internal to `CleansiaCore` — single mutation path = the spine; see the
  living doc §"Session-presence on the public app surface").
- **One authed generated call: `PartnerDashboardAPI.dashboardGetStats`**, going out **through the
  Core-spine-backed `RequestBuilderFactory` (ADR-0019)** — proving the Bearer + `X-Device-Id`/`X-Device-Label`/
  `X-Time-Zone` headers + the single-flight 401-refresh reach a **generated** call (the whole point — it 401s
  tokenless without the factory).
- **`firstName` / `employeeId` via a one-shot `employeeGetCurrentEmployee` on appear** (iOS has **no**
  `UserProfileStore` yet — that is T-0304's shell concern; the one-shot avoids standing up a profile store in
  the proving vertical).
- **The stats-driven cards**: greeting bar + **Weekly earnings** + **Pay period** (with its progress
  bar) + **Last month** + the **hero with all 3 states IMPLEMENTED** (next-job / available-work / empty),
  driven by `dashboardGetStats`-derivable data. **The 3 hero states themselves are in T-0303 scope and
  shipped; the live non-empty next-job/available-work *data* feeds land in T-0307** (see DEFERRED below).
  Gate-DP applies — cites `DashboardScreen.kt`, native SwiftUI, iOS-wins-on-conflict.
- **Explicit `UiState` rendering**: `loading` / `loaded` / `error` (the three E1 states, ADR-0014 D2′) with
  `ObservableObject`/`@Published`.
- **Simple load-on-appear**; **navigation actions wired as INERT closures** (the cards are present and tappable
  but route nowhere yet — the destinations are T-0304+).

**DEFERRED — explicitly out of T-0303, with the ticket each lands in:**
- **Upcoming-orders + available-jobs-preview endpoints** (the 2 non-critical dashboard sub-calls) and the
  **live non-empty next-job hero data** → the hero **states** ship in T-0303 (all 3, from
  `dashboardGetStats`-derivable data); only the **live upcoming/preview *data* feeds** land with **T-0307**
  (partner order work-loop) where OrdersList exists. (To be explicit for the reviewer: states = T-0303,
  live data = T-0307.)
- **Silent-stale 60s caching + the dedup mutex + the singleton snapshot repo** (`DashboardRepository`'s caching
  layer) → **T-0304** (the shell, where tab-survival caching first matters) — T-0303 does a plain load-on-appear.
- **Pull-to-refresh** (the suds indicator + user-pull vs background-refresh routing) → **T-0304**.
- **The unread-notifications DB feed + the bell badge** → **T-0310** (partner profile/notifications) — the bell
  renders inert (no badge) in T-0303.
- **The 6 cards' real navigation destinations** (Orders, Earnings, Profile, Documents, Notifications) → wired as
  the corresponding screens land: **T-0304** (shell tabs), **T-0307** (orders), **T-0309** (earnings),
  **T-0310** (profile/documents/notifications). T-0303 ships them as **inert closures**.
- **Quick-actions grid** (Availability / Pay history / Documents / Help tiles) → **T-0304/T-0310** (its targets
  don't exist yet); omit from T-0303 or render inert — does not prove the spine.

**Why this is right (not under-scoping):** the proving vertical must exercise **login → token store → device/
time-zone headers → an authed *generated* business call → the 401-refresh → `UiState` rendering** with **zero**
dependencies on caching, a notifications DB, or screens that don't exist. `dashboardGetStats` through the
ADR-0019 factory does exactly that. Everything deferred is *additive parity* that lands when its home screen
lands — and is reached by **copying** the ADR-0019 seam, not re-deciding it. This is a **scope record, not an
ADR** (no new decision/trade-off beyond ADR-0013 D9 + ADR-0019, which own the calls).

### 7.3 T-0303 — acceptance evidence + security forward-notes (recorded 2026-06-26, on close)

**Commits (`phase/ios-phase1`):** `8996df9` (Slice A — partner login spine) + `2a57f70` (Slice B —
read-only Dashboard), preceded by `d965c5b` (ADR-0019 + the §7.2 scope record) and `8d4cfe3` (the T-0302
codegen toolchain first real generation). Owner commits these backlog edits to the phase branch (PM does not
commit).

**Acceptance evidence (AC ↔ proof):**
- **The proving vertical works end-to-end** — partner login via the hand-written `AuthClient`
  (empty-token / unverified-email gate; router gates **verified → dashboard** vs **unverified →
  `verifyEmail` placeholder**) → an authed **read-only Dashboard** (greeting + Weekly-earnings / Pay-period /
  Last-month stats cards + the **3-state hero**) driven by the generated `dashboardGetStats`.
- **ADR-0019 generated-client auth adapter implemented + proven** — reviewer **check #13-gen PASS** (single
  token source; no per-call header/token code outside `HeaderAdapter`; no hand-edited generated client) and
  the **TC-IOS-GEN** test passes: a generated call carries **Bearer + `X-Device-Id` / `X-Device-Label` /
  `X-Time-Zone`** despite the generated `requiresAuthentication:false`, and a **401 drives a single-flight
  refresh + exactly one retry** with the rotated token.
- **The required router-gate test is present** — the §7.2 router acceptance item:
  `requiresEmailConfirmation == true` → `verifyEmail` (the sibling of **TC-IOS-EMPTYTOKEN**, §8).
- **Gates green** — `swiftformat --lint` + `swiftlint --strict` clean; **CleansiaCore 93 tests +
  CleansiaPartner 17 tests** pass on the **iPhone 17 simulator**; **reviewer AND security APPROVE on both
  slices**.

**Security forward-notes — for the later authed waves that copy the ADR-0019 seam (record + carry):**
1. **Customer-wave factory is host-specific.** When the **CUSTOMER** wave copies the generated-client auth
   seam, it must install its **OWN** `RequestBuilderFactory` into `CleansiaCustomerApiAPI` with the
   **CUSTOMER allow-list** — the 401 detector currently lives in the **partner** factory; the bridge itself
   is **host-agnostic**, but the factory install + allow-list are per-host. (Carries to **T-0312** customer
   shell+auth; relates to the dual-use `Order`/`Payment` allow-list checkpoint **T-0332**.)
2. **`employeeId` round-trip is safe only because the backend overrides it.** The iOS client round-trips its
   own **server-derived** `employeeId` to `dashboardGetStats`; this is safe **ONLY** because the backend
   `GetDashboardStats` handler **overrides the client `EmployeeId` for non-admin callers**. Record this as a
   **standing dependency** for later authed waves — any wave that copies the seam and round-trips a
   server-derived id must rely on the same server-side override (never trust the client-supplied id for
   authz scoping).

### 7.4 T-0304 (Phase-2 partner shell + SplashGate + RegistrationLock) — acceptance scope + the three Understand-pass rulings (recorded 2026-06-26, architect)

> **CONFIRMED-AS-SHIPPED + CLOSED 2026-06-26 — T-0304 is `done`** (§3 / status-log;
> `55b39aa`+`c269360`+`df71181` on `phase/ios-phase2`). What shipped matches this scope record exactly.
> **Acceptance evidence (AC ↔ proof):**
> - **Slice A (the fail-closed gate, `c269360`)** — a `SplashGate` decision tree + a fail-closed
>   `RegistrationLock`. Decision 1 is honored end-to-end: the predicate is the **AND** of
>   `hasCompletedProfile && areDocumentsUploaded && (contractStatus == .approved(4) || .active(2))`, **any
>   nil/unknown/other → LOCKED**, **availability NOT read as a clause**; **BOTH error paths fail CLOSED** —
>   SplashGate `.failure` → lock (never the shell), and the lock VM's `.failure` **preserves** the
>   last-known/Missing state and **never** unlocks (the success-only "complete" watermark unlocks).
>   **Reviewer #24 + TC-IOS-REGLOCK green; security APPROVE** — security traced the backend and confirmed
>   `CheckCurrentEmployee` is **token-scoped + `[Permission]`-guarded with no client-supplied id**. The
>   **ADR-0020 router** (Decision 2, reviewer #23) **reseeded `.dashboard` → `.splash`**, which **CLOSED a
>   latent T-0303 fail-OPEN** the architect caught (an authed-but-incomplete partner previously landed
>   straight on the authed area). The closed **14-token `missingFields` vocabulary**
>   (`Employee.GetMissingProfileFields`) is **localized ×5**.
> - **Slice B (the shell, `df71181`)** — a native SwiftUI `TabView` (ADR-0018 D3) with the **4 tabs in the
>   Android `MainTab` order** (Dashboard·Orders·Invoices·Profile); the `.dashboard` tab hosts the **T-0303
>   `DashboardView`** (now a tab); the dashboard's **`onOpenOrders` switches to the Orders tab**; the 3 other
>   tabs are **shared placeholders**. **Gate-DP APPROVE** — the native `TabView` divergence from the Android
>   floating-island pill is the **sanctioned ADR-0018 D3 component swap** (component-only, noted; layout/flow
>   unchanged).
> - **ADR-0020 docs (`55b39aa`)** — the partner router pattern canonicalized so T-0305+ don't reinvent it.
> - **Gates green** — `swiftformat --lint` + `swiftlint --strict` clean; **CleansiaCore 93 + CleansiaPartner
>   61** tests pass on the **iPhone 17 simulator**; **reviewer + the applicable security/Gate-DP gate ALL
>   APPROVE** on both slices.
>
> **The two §7.4-open choices — RESOLVED (developer-confirmed):**
> - **(a) The Rejected-row contact-support affordance shipped INERT** (no `mailto:`) — the §7.4 inert option
>   below. The `registration_lock_action_contact_support` translation is **carried in the catalog** for when
>   **T-0310** wires it.
> - **(b) The lock's silent-stale caching was DEFERRED** — plain **load-on-appear + Retry + `.refreshable`**
>   (the §7.4-sanctioned "OR defer" option below); the `STALE_WINDOW` caching lands later alongside the
>   dashboard's deferred cache.
>
> **Deferrals (all homed, confirmed deferrals not cuts):** the lock "Fix" CTAs are **inert → T-0310**
> (profile-section chain); the SplashGate **onboarding branch is deferred → T-0305**; the pre-existing
> hardcoded "Verify your email — coming in T-0305" placeholder string (`PlaceholderVerifyEmailView`, from
> T-0303) **localizes when T-0305** builds the real ConfirmEmail screen. Resulting transition: **T-0304 →
> `done`; next runnable = T-0305.**

T-0304 builds the partner **authenticated shell** (a SwiftUI `TabView`: Dashboard·Orders·Invoices·Profile —
Android `MainScaffold.kt:44-49` parity) **gated by a SplashGate + a RegistrationLock that FAILS CLOSED**.
The Understand pass surfaced three decisions; all three are ruled below. **Two are records (no new
trade-off): Decision 1 (the gate placement + fail-closed semantics) confirms the Android partner gate, and
Decision 3 (scope vs Android) applies the T-0303 inert-nav precedent + ADR-0013 parity. Decision 2 (the
router shape) is a genuine judgment call canonicalized as ADR-0020** (the partner router pattern, so
T-0305+ don't reinvent it). Android parity sources (verified): the shell `MainScaffold.kt`; the SplashGate
decision tree `navigation/PartnerNavHost.kt:74-97` + `:448-509` (`SplashViewModel`); the fail-closed
predicate `features/orders/RegistrationLockViewModel.kt:103-109` + the fail-closed-on-error preservation
`:197-211`; `RegistrationLockScreen.kt` (the locked screen). Status read via
`PartnerEmployeeAPI.employeeCheckCurrentEmployee()` → `RegistrationCompletionStatus`, through the ADR-0019
Core-spine generated-client seam (already installed).

#### Decision 1 — RegistrationLock placement + fail-closed semantics (SECURITY — the standing gate every later partner wave sits behind)

**RULING: CONFIRMED as briefed. This is a confirmation of the Android partner gate (ADR-0013 "mirror the
code") — no new ADR.** Verified against the Android source:

- **The gate sits BETWEEN login and the authed shell.** The shell (Orders/etc.) is **unreachable** until
  `isRegistrationComplete == true`. Structurally enforced by ADR-0020: the router resolves `.splash` (the
  gate) **before** rendering `.dashboard` (the shell); there is no login→shell path bypassing `.splash`.
  (Android: `startDestination = Splash`, `PartnerNavHost.kt:72`; the shell `Main` is reached **only** from
  `Authenticated` or the lock's `onCompleted`, `:74-97` / `:197-201`.)
- **The predicate is an AND, mirroring Android** (`RegistrationLockViewModel.kt:103-109`, `isRegistrationComplete()`):
  `hasCompletedProfile == true && areDocumentsUploaded == true && (contractStatus == .approved(4) ||
  contractStatus == .active(2))`. **Any nil/unknown/other → false → LOCKED** (the Kotlin `== true` on the
  nullable booleans gives nil → false; `ContractStatus`: Pending=1/Active=2/Terminated=3/Approved=4/Rejected=5).
  **Availability is NOT a gate clause** — backend always reports it true; the iOS predicate must not read it
  (the Android comment at `:106-107` makes this explicit).
- **BOTH error paths fail CLOSED:**
  - **(i) SplashGate** — a registration-status API **`.failure` routes to RegistrationLock, never to the
    shell** (the `SplashViewModel` `ApiResult.Error → NeedsRegistrationLock` parity, `PartnerNavHost.kt:506`).
  - **(ii) The lock VM's `.failure` PRESERVES the last-known/Missing state and never flips to unlocked** —
    the Error branch must **not** touch the cached `status`, so the last-known categories (or all-Missing on
    a first-attempt failure) stay rendered and the gate stays locked (the Android Error branch at
    `RegistrationLockViewModel.kt:197-211` does exactly this; the comment "never accidentally unlock a
    half-onboarded cleaner on a transient network blip" is the load-bearing intent). **The success-only
    unlock** is when the "complete" watermark fires (Android `RegistrationLockScreen.kt:112-114`,
    `LaunchedEffect(status){ if isRegistrationComplete -> onCompleted }`).

**Reviewer check (the iOS sibling of the partner-gate rule) — #24:** the partner registration gate is
**fail-closed end-to-end**: (a) the predicate is the **AND** of profile + documents + contract∈{Approved,
Active}, with **every** nil/unknown/other → LOCKED (no `??true`, no optional-defaulting-to-permissive); (b)
the SplashGate routes a status-API `.failure` to the lock, **never** the shell; (c) the lock VM's `.failure`
**preserves** the cached status (does not clear it) and **never** transitions to unlocked — the only unlock is
the success-path "complete" watermark; (d) availability is **not** read as a gate clause. A permissive default
on any nil field, a `.failure` reaching the shell, or a `.failure` clearing/unlocking the gate is a
**blocking security finding**.

**SECURITY forward-note (carry to every later partner wave):** the RegistrationLock is the **standing gate
every later partner wave sits behind** (T-0307 orders, T-0309 pay, T-0310 profile). Those waves render
**inside** `.dashboard` (the shell), which is reached **only** past this gate — so they inherit the gate and
must **not** add a second, weaker status check or a permissive default that could re-open it. The gate's
status call rides the **ADR-0019** generated-client seam (and the §7.3 forward-note #2 applies: any
server-derived id round-trip is safe only because the backend overrides it for non-admin callers).

**Required test contract — TC-IOS-REGLOCK (red-first, T-0304):**
- **empty/nil status → LOCKED** (an all-nil `RegistrationCompletionStatus` is not complete);
- **each single-wrong field → LOCKED** — profile false (others ok) → locked; docs false → locked;
  contract Pending(1)/Terminated(3)/Rejected(5)/nil → locked (only Approved(4) or Active(2) passes);
- **`.failure` → LOCKED** — SplashGate: a status-API `.failure` resolves to `.registrationLock` (not
  `.dashboard`); the lock VM: a refresh `.failure` preserves the prior `status` and stays locked;
- **only profile+docs+Approved|Active → UNLOCKED** — the complete state (and only it) fires the watermark →
  `.dashboard`.
(The SplashGate `.failure`/no-session/complete/incomplete cases are shared with **TC-IOS-SPLASH-RESOLVE**,
ADR-0020.)

#### Decision 2 — Router shape: the flat-enum `PartnerRootView` root-switch, gated by `.splash` (→ ADR-0020)

**RULING: the flat-enum `PartnerRootView` approach is the SANCTIONED iOS partner router — RECORDED AS
ADR-0020** (a genuine judgment call: two valid readings — flat-enum root-switch vs path-based
`NavigationStack` audience router — and the proving vertical already chose one; T-0305+ would each
re-decide it otherwise). The decision (full trade-off + alternatives + reviewer #23 in the ADR):
- Extend the T-0303 `enum Route` with **`.splash`** (the decision state) and **`.registrationLock`**; the
  `.dashboard` case becomes the **shell** (the `TabView`).
- **Seed `hasValidSession ? .splash : .login`** (NOT `.dashboard` — the T-0303 seed is a fail-OPEN hole once
  the gate exists; this closes it, Android `startDestination = Splash`).
- **A verified login routes to `.splash`** (the Android "bounce through Splash so registration re-checks",
  `PartnerNavHost.kt:118-124`), which **re-resolves** to `.dashboard`-shell vs `.registrationLock` vs
  `.login`. **`.verifyEmail` is preserved** for the `requiresEmailConfirmation == true` branch (the T-0303
  §7.2 router-gate test is **extended, not broken**).
- **`NavigationStack` stays the intra-audience push container** (OrderDetail, ProfileSection, the
  onboarding-chain sections), **not** the audience selector.
- **Rejected:** a path-based `NavigationStack` audience router (discards the working T-0303 root-switch +
  §7.2 gate test; models replace-semantics audience hops as awkward path push-then-clear; the audience is a
  small **closed** set an `enum` checks exhaustively). See ADR-0020 Alternatives.

**Reviewer check #23 (ADR-0020)** applies to T-0304: the top-level audience is the flat-enum root-switch
seeded `.splash`/bounced-through-`.splash`; no top-level audience state modeled as a pushed path; no
login→shell path bypassing `.splash`.

#### Decision 3 — T-0304 scope vs Android (parity judgment): the deferral map

**RULING: CONFIRMED — T-0304 ports the gate + predicate + locked screen + sign-out + pull-to-refresh/Retry;
the "Fix" CTAs and the onboarding branch are INERT/deferred (the T-0303 inert-nav precedent, §7.2).** No new
trade-off (ADR-0013 parity + the §7.2 inert-closure precedent own the call) — a **scope record**, not an ADR.

**IN — T-0304 acceptance scope:**
- **The partner shell** — the SwiftUI `TabView` (Dashboard·Orders·Invoices·Profile), Android `MainScaffold.kt:44-49`
  parity. **The Dashboard tab reuses the T-0303 `DashboardView`** (now a tab, not the bare root); **the other
  three tabs may be minimal/placeholder** (their real content is T-0307 orders / T-0309 invoices / T-0310
  profile — see the deferral map). Gate-DP applies (cite `MainScaffold.kt`, native SwiftUI `TabView`,
  iOS-wins-on-conflict per ADR-0018: Compose `NavigationBar` → `TabView`, same tabs/order).
- **The router** — `PartnerRootView` extended per **ADR-0020** (`.splash`/`.registrationLock` cases, the
  `.splash`-seed + login-bounce, the shell as `.dashboard`).
- **The SplashGate** — a `SplashGateViewModel` (the `SplashViewModel` parity, `PartnerNavHost.kt:478-509`)
  resolving the gate **once** on appear → `.dashboard`/`.registrationLock`/`.login`, **fail-closed on
  `.failure`** (Decision 1). For T-0304 the splash resolves the **authed** branch; the
  `NeedsOnboarding` branch is deferred (below).
- **The RegistrationLock screen** — the hero lock + **3 category rows (Profile / Documents / Approval) +
  progress bar** (Android `RegistrationLockScreen.kt`), **sign-out**, and **pull-to-refresh + Retry banner**
  (the `userRefresh` path, `RegistrationLockViewModel.kt:143-144` + the error banner). The **fail-closed
  predicate + semantics** (Decision 1) + **TC-IOS-REGLOCK**.
- **The silent-stale caching** of the lock's status (the `STALE_WINDOW_MS`/`ensureFreshOrCachedAsync` path,
  `RegistrationLockViewModel.kt:164-168`) — port at parity, OR defer to T-0307 alongside the dashboard's
  silent-stale caching that §7.2 deferred to T-0304 — **the dev/reviewer picks one and notes it in-ticket**;
  the *user-pull/Retry* path (which drives the visible indicator) is IN regardless.

**DEFERRED — explicitly out of T-0304, with the ticket each lands in (the deferral map):**
- **The lock's "Fix" CTAs** (the per-row chevrons routing into the profile-section editors —
  `RegistrationLockViewModel.kt:279-293` `fixDestination`) → render the rows + chevrons but the CTAs are
  **INERT closures** (the T-0303 inert-nav precedent, §7.2): present and visible, route nowhere yet. The
  **profile-section onboarding chain** they target lands in **T-0310** (profile section editors +
  onboarding-chain) — the Profile-row CTA — and the Documents-row CTA also homes to **T-0310** (`ProfileDocuments`).
  The **Rejected-approval `mailto:` support** affordance (`RegistrationLockScreen.kt:419-428`) may ship inert
  or as a simple mail link in T-0304 (it has no in-app destination); note the choice in-ticket.
- **The SplashGate `NeedsOnboarding` branch** (Android's 4th `SplashOutcome`,
  `PartnerNavHost.kt:86-90`/`:486-491` — first-launch onboarding for a session-less, not-yet-onboarded user)
  → **T-0305** (partner auth completeness — Register/Forgot/ConfirmEmail/**Onboarding** chain). For T-0304
  the splash's no-session branch resolves to `.login` (onboarding-vs-login is T-0305's split); ADR-0020 D5
  records this.
- **The lock-complete onboarding-chain re-fetch loop** (Android's `nextOnboardingDestination` chaining,
  `RegistrationLockViewModel.kt:338-348`, that walks the cleaner section-by-section without bouncing through
  the lock) → **T-0310** (the profile chain that owns the section editors). T-0304's lock unlocks on the
  watermark (success path) but does not drive the forward chain.

**Why this is right (not under-scoping):** T-0304's job is the **standing gate + the shell scaffold** — the
security boundary every later partner wave sits behind. It must prove **login → splash → fail-closed
gate → shell-vs-lock** end-to-end with the predicate + both fail-closed paths correct. The "Fix" CTAs and
the onboarding branch are **additive parity** that land when their destination screens (T-0305 onboarding,
T-0310 profile chain) exist — reached by **copying** the ADR-0020 router seam + the §7.4 gate, not
re-deciding them. Rendering them inert (the §7.2 precedent) keeps the locked screen visually at parity while
honestly deferring the destinations that don't exist yet.

### 7.5 T-0305 (Phase-2 partner auth completeness — Register/Forgot/ConfirmEmail/Onboarding) — acceptance scope + the five Understand-pass rulings (recorded 2026-06-26, architect)

T-0305 completes the partner auth surface: **Register**, **Forgot password**, **ConfirmEmail** (+ resend),
and the **first-launch Onboarding** branch the §7.4 deferral map homed here. All four auth paths are
**hand-written, anonymous, excluded from codegen** per the header-parity-contract (`Auth.swift` spine — the
existing login/refresh/logout get register/confirmEmail/resendConfirmation/forgotPassword added). The four
paths are **already** in `AnonymousAllowList.sharedAuth` (header-parity-contract §3). **Gate-DP applies** —
each screen cites its Android Compose counterpart (`RegisterScreen.kt`, `ForgotPasswordScreen.kt`,
`ConfirmEmailScreen.kt`, `OnboardingScreen.kt`) and is built native-SwiftUI, iOS-wins-on-component-conflict.
The five rulings below **compose the accepted ADRs** (0013 port, 0014 floor/state, 0018 Gate-DP, 0019
generated-client auth, 0020 partner router) + the header-parity-contract — **no new ADR is opened** (none
introduces a genuinely new trade-off; each is an application of an accepted decision, recorded here + in the
living doc + `patterns-mobile.md`). Android parity sources (verified): the auth VMs
`features/auth/{RegisterViewModel,ForgotPasswordViewModel}.kt`; the auth repo (login/confirm/register/resend/
forgot signatures + outcomes) `data/auth/AuthRepository.kt`; the router confirm/onboarding wiring
`navigation/PartnerNavHost.kt`; the settings store `core/settings/AppSettingsRepository.kt`; the shared
`:core` password-rule widget `core/ui/components/PasswordRuleList.kt`.

> **CONFIRMED-AS-SHIPPED + CLOSED 2026-06-26 — T-0305 is `done`** (§3 / status-log;
> `ccd25cd`+`e232147`+`3e70cdb`+`84d38bc` on `phase/ios-phase2`, 4 slices). What shipped matches the five
> rulings below. **Acceptance evidence (AC ↔ proof):**
> - **All four flows shipped.** **Register** (+ the Core `PasswordPolicy`/`PasswordRuleList` — Decision 4);
>   **Forgot password** (single-phase); **ConfirmEmail** (**replaces** the `PlaceholderVerifyEmailView`
>   placeholder; **reuses the LIVE empty-token gate** — `200`+empty Token → `unverifiedEmail` / **no app
>   entry**, `200`+token → **authenticated** — Decision 3.5, NOT a parallel confirm gate); **Onboarding** (a
>   2-page **pre-auth** intro + the SplashGate **onboarding branch** + `hasSeenOnboarding` in the **new** Core
>   `AppSettingsStore` — Decision 1). The login screen's **forgot + sign-up links are both now LIVE**, and
>   `.verifyEmail(email:)` **carries the email** (Decision 2 — **no `UserProfileStore` was introduced**).
> - **Security (Slice A `e232147`, reviewer #25) — APPROVE.** The spine's `send()` gained an **`httpMethod:`**
>   param (`ConfirmUserEmail` is **PUT** — no silent 405); the **Bearer is withheld on the anon confirm path**
>   (the **double-skip** — a token *is* present post-login, but the path is anon → no `Authorization`) and
>   security verified that is **SAFE** by **tracing the backend `ConfirmUserEmail` handler**, which resolves
>   the user from the confirmation **CODE alone** (no session identity needed). **No new anon allow-list
>   entry; `Logout` stays AUTHED.** A **positive-control test** proves the double-skip assertion is
>   **non-tautological** (it would catch a Bearer that *did* leak). The device/tz headers are still sent.
> - **Slices C+D (`84d38bc`, Forgot + Onboarding) — explicit gate-safety review: SAFE, no escalation.** The
>   onboarding branch is **pre-auth only** and resolves no-session → `.unauthenticated`/`.needsOnboarding`,
>   never `.authenticated`.
> - **F1 (Decision 5):** iOS **LOCALIZES ×5** the validation strings the Android partner Register/Forgot VMs
>   hardcode in English — **iOS does it right; the Android bug is NOT replicated.** The android fix is filed
>   as the **PM follow-up T-0333** (small, mechanical i18n; **independent** of the iOS wave — does not block
>   it and is not blocked by it).
> - **Gates green (§8):** `swiftformat --lint` + `swiftlint --strict` clean; **CleansiaCore 114 +
>   CleansiaPartner 96** tests pass on the **iPhone 17 simulator** (the suites grew with **TC-IOS-CONFIRM-PUT**,
>   **TC-IOS-SETTINGS**, **TC-IOS-PASSWORD-POLICY**, and the extended **TC-IOS-ANON / -EMPTYTOKEN /
>   -VERIFY-EMAIL-ARG**); **reviewer #25 + #26 PASS** on every slice, **security-APPROVE on Slice A**.
>
> **Seed refinement (ADR-0020 living-doc fold-in — recorded, NO new ADR):** the `PartnerRootView` launch seed
> is now **UNCONDITIONALLY `.splash`** (was `hasValidSession ? .splash : .login`), so the **SplashGate is the
> SOLE launch resolver** — needed for the **onboarding-vs-login** decision on an un-authed **first run** (the
> old `: .login` seed short-circuited the onboarding branch). **The fail-closed registration gate (#24) is
> BYTE-UNCHANGED and no bypass is introduced:** the no-session branch resolves only to
> `.unauthenticated`/`.needsOnboarding` (via `hasSeenOnboarding`), **never `.authenticated`** — an
> authed-but-incomplete partner still cannot reach the shell (the fail-OPEN T-0304 closed stays closed). **It
> refines, not contradicts, ADR-0020 D2** (the `.splash`-sole-resolver / login-bounce posture). A one-line
> note recording this is folded into `architecture/decisions/ios-app-architecture.md` under the
> ADR-0020/partner-router section.

#### Decision 1 — iOS device-local settings store: a GENERAL `AppSettingsStore` in `CleansiaCore` (UserDefaults-backed)

**RULING: introduce a minimal, GENERAL `AppSettingsStore` in `CleansiaCore` (UserDefaults-backed), NOT a
single-purpose `OnboardingStateStore` + a separate language helper. No new ADR** — this is an application of
ADR-0013 D1 (CleansiaCore is the `:core` parity, the home for shared device-local infra) + the existing
`CleansiaCore/Validation` precedent (e.g. `EmailValidator.swift` already hoisted to Core), recorded here +
`patterns-mobile.md` + the living doc as **"the one way to do device-local settings on iOS."**

- **Surface (parity with Android `AppSettingsRepository.kt`):** `hasSeenOnboarding` (a get + `markSeen()` —
  the `hasSeenOnboarding()`/`markOnboardingSeen()` parity, `AppSettingsRepository.kt:28-33`,
  `booleanPreferencesKey("onboarding_seen")` `:25`) + a **resolved language tag** ∈ `{en,cs,sk,uk,ru}`
  (the `LANGUAGE` setting parity, `AppSettingsRepository.kt:24`/`:41-43`). The resolver: read the persisted
  tag if present and in-set; else **seed from `Locale.current.language.languageCode`** if in-set; else
  **default `"en"`** (the Android `?: "en"` fallback, `RegisterViewModel.kt:89` / `ForgotPasswordViewModel.kt:56`).
- **Storage = `UserDefaults`, NOT Keychain.** This is the deliberate parity with Android's DataStore
  **wiped-on-uninstall** semantics (`partner_app_settings`, `AppSettingsRepository.kt:13`). The
  device-id/token Keychain (header-parity-contract §2/§6) is for the security-load-bearing per-install id +
  the session — onboarding-seen + a UI language preference are **not** secrets and must reset on reinstall,
  exactly like DataStore. (Putting them in Keychain would make "seen onboarding" survive an uninstall, the
  wrong behavior, and pollute the secret store.)
- **General, so T-0307+ / customer reuse it.** The store is a small key/value façade (not onboarding-only) so
  later device-local prefs (theme, the dashboard `STALE_WINDOW` cache flags, customer prefs in T-0312+) add a
  property here instead of standing up a new store each time. Theme is **out of T-0305 scope** (no theme
  picker yet) but the store is shaped to take it without a redesign.
- **What T-0305 consumes it for:** (a) the SplashGate **onboarding branch** reads `hasSeenOnboarding` to
  resolve no-session → `.onboarding` vs `.login` (Android `SplashViewModel`, `PartnerNavHost.kt:487`); the
  OnboardingScreen calls `markSeen()` on finish (Android `OnboardingScreen` "mark as seen" effect,
  `PartnerNavHost.kt:99-107`); (b) Register/Forgot/Resend read the **language tag** to send the email-locale
  `language` field (Android `RegisterViewModel.kt:89`, `ForgotPasswordViewModel.kt:56`, the repo
  `register(...,language)` / `forgotPassword(email,language)` / `resendConfirmation(email,language)`,
  `AuthRepository.kt:37-52`). **CRC:** new `ios-app-settings-store` role (below).

#### Decision 2 — ConfirmEmail email threading: through the Route ASSOCIATION VALUE (`.verifyEmail(email:)`), NOT a `UserProfileStore`

**RULING: thread the email through the Route associated value — `case verifyEmail(email: String?)` — NOT by
building an iOS `UserProfileStore`. No new ADR** — ADR-0020 D5 explicitly sanctions "a future top-level
audience state is a new `enum` case under this ADR (a living-doc fold-in)"; here the existing `.verifyEmail`
case merely **gains an associated value**, which is lighter than and aligned with the ADR-0020 "the audience
enum carries the state" posture. Recorded as an ADR-0020 **living-doc fold-in** (the `patterns-mobile`
`navigation.Routes` top-level-audience row + the living doc note), **not** a superseding ADR.

- **Why not a `UserProfileStore`.** Android reads the resend email from `UserProfileStore` (persisted by
  login even on the unconfirmed path — `AuthRepository.kt:104-105`/`:120-137`, and by confirm `:182-183`).
  iOS has **no `UserProfileStore`** (T-0303 deliberately used a one-shot `employeeGetCurrentEmployee`,
  §7.2, to avoid standing one up). Building a whole profile store **just** to carry one email to the verify
  screen is over-build; the email is a single nav input, which is exactly what an associated value is for.
- **What changes (minimal):** the empty-token/unverified login already **has** the email —
  `LoginOutcome.unverifiedEmail(email:hasToken:)` (the Android `LoginOutcome.UnverifiedEmail(email,hasToken)`
  parity, `AuthRepository.kt:99-102`/`:110-114`/`:277`). The iOS `LoginSuccess`/`afterLogin` currently
  **drops** it (`Route.afterLogin` branches on `requiresEmailConfirmation` only). T-0305: (a) `LoginSuccess`
  carries the email; (b) `Route.afterLogin` seeds `.verifyEmail(email:)` from it; (c) the verify screen's
  resend uses that email. **Confirm-code itself needs no email** — `confirmEmail(code:)` posts only the code
  (Android `AuthRepository.confirmEmail(code)` → `ConfirmUserEmailCommand(code)`, `:168-171`); the email is
  needed **only** for resend.
- **The cold-start-mid-confirm-with-no-email case degrades to Android's behavior.** If the app is killed and
  relaunched directly into `.verifyEmail` with **no** email in hand (the associated value is `nil` — hence
  `email: String?`), resend has no address: **disable/guard resend and show `error_generic`**, mirroring
  Android's `UnverifiedEmail(email = "", hasToken = false)` blank-email guard
  (`AuthRepository.kt:99-102`/`:175-180`). The user can still enter the code (which doesn't need the email),
  or back out to Login and re-trigger. **CRC:** the `ios-partner-root-router` (ADR-0020) is unchanged in
  responsibility — it still just *lands the state*; the state now carries a payload (the email) it does not
  interpret.
- **Reviewer angle:** no `UserProfileStore` appears in `CleansiaCore` or the partner target for T-0305; the
  email rides `.verifyEmail(email:)`; the `requiresEmailConfirmation == true → .verifyEmail` gate (T-0303
  §7.2 / ADR-0020 D3) is **preserved** — the associated value is additive, the gate predicate unchanged.

#### Decision 3 — Auth endpoint/allow-list + the PUT method (SECURITY confirmation + a small plumbing rule)

**RULING: CONFIRMED on all four points. This confirms the header-parity-contract §3/§5 and the
already-built `AnonymousAllowList.sharedAuth` — no new allow-list entry, one small spine change (the PUT).**

1. **No new anon allow-list entry is needed.** All four T-0305 paths — `/api/Auth/Register`,
   `/api/Auth/ConfirmUserEmail`, `/api/Auth/ResendConfirmationEmail`, `/api/Auth/ForgotPassword` — are
   **already** in `AnonymousAllowList.sharedAuth` (header-parity-contract §3 table; T-0300 `AnonymousAllowList.swift`).
   The **partner host stays auth-only** (the guest-booking surface is customer-host-only). ✓
2. **`/api/Auth/Logout` stays AUTHED (unchanged).** It is `[Authorize]` on both hosts and carries the refresh
   token in the body to revoke (header-parity-contract §3 note); it is **not** added to the allow-list. ✓
3. **`ConfirmUserEmail` is PUT — the spine's `send()` must gain an HTTP-method param (default `.post`).**
   The header-parity-contract §3 table lists `ConfirmUserEmail` as **PUT** (and `ForgotPassword`/`Register`/
   `ResendConfirmationEmail` as POST). The current hand-written `send()` hardcodes POST — **missing the
   method param is a silent 405**. T-0305 adds an `httpMethod:` parameter to the spine's request builder
   (default `.post`, so login/refresh/register/resend/forgot are unchanged) and confirmEmail passes `.put`.
   This is a one-parameter plumbing change to the hand-written spine, **not** a contract or codegen change. ✓
4. **Confirm rides the AUTHED session, but the Bearer is correctly SKIPPED by `HeaderAdapter` (the
   "double-skip") — verified correct per header-parity-contract §3.** Login persisted a Bearer on the
   unverified path (the token-present branch, `AuthRepository.kt:104-114` parity), so a stored Bearer exists;
   but `/api/Auth/ConfirmUserEmail` is on the anon allow-list, so the **`HeaderAdapter` withholds the
   `Authorization` header** even though a token is stored (the §3 path-skip rule: "skip the Authorization
   header entirely when the path matches an anon endpoint — even if a stale/revoked access token is
   stored"). The device/tz headers (§1) are **still** sent. This double-skip (token present + path anon →
   no Bearer) is **intended**: the server's confirm endpoint is `[AllowAnonymous]` and the gate is the
   6-digit code, not the session.
5. **`confirmEmail` reuses the existing empty-token gate (header-parity-contract §5, the T-0303
   `LoginOutcome`/`TC-IOS-EMPTYTOKEN` machinery — NOT a new gate).** On the confirm `200` body:
   `200 + empty/blank Token` → `LoginOutcome.unverifiedEmail(hasToken: false)` (Android's
   `UnverifiedEmail(email, hasToken=false)`, `AuthRepository.kt:175-180`) → **no app entry**, show
   `error_generic` and re-prompt; `200 + non-empty Token` → persist the token bundle + route as
   `authenticated` → the verified login bounces through `.splash` (ADR-0020 D3) → registration gate. The
   confirm path uses the **same** empty-token gate the login path already uses (§5 "Login **and**
   ConfirmUserEmail" — the gate is shared); it does **not** add a parallel confirm-specific gate.

**SECURITY forward-note (for the T-0305 security gate on the auth slice):** the security agent verifies, on
the slice, (i) the **allow-list predicate** is unchanged + complete + host-correct (no new partner anon
entry; Logout still authed) — reviewer #4 / TC-IOS-ANON sibling; (ii) the **PUT** is wired for confirm (no
silent 405) and the method param defaults `.post` so no other path flipped method; (iii) the **double-skip**
holds — a stored Bearer is **not** attached on the four anon paths (TC-IOS-ANON covers it; confirm is the
sharpest case because a token *is* present); (iv) the **empty-token gate is reused** (200+empty →
unverified, no session; 200+token → authenticated) — TC-IOS-EMPTYTOKEN extended to the confirm path. This is
not a full security-gate ticket (the spine T-0300 carried that); it is a **slice check** that T-0305 didn't
regress the spine's anon/PUT/empty-token contract.

#### Decision 4 — Password policy: a Core `PasswordPolicy` validator (≥8 && ≥1 letter && ≥1 digit) feeding a Core `PasswordRuleList`

**RULING: extract a `PasswordPolicy` validator into `CleansiaCore/Validation` (the exact Android predicate),
+ a `PasswordRuleList` Core component driven by it; harvest into `patterns-mobile.md`. No new ADR** — this is
an application of ADR-0013 D1 (shared mobile logic lives in CleansiaCore) + the existing
`CleansiaCore/Validation/EmailValidator.swift` precedent, and a **catalog harvest** (per the
pattern-evolution loop) of two artifacts Android already shares in `:core`.

- **The exact predicate (Android `RegisterViewModel.kt:37-39`, consumed `:73`):** `password.count >= 8 &&
  password.contains(where: \.isLetter) && password.contains(where: \.isNumber)`. iOS reuses the **same**
  predicate verbatim — a behavior difference here would be a parity bug. Backend (`BaseAuthValidator.cs` /
  `Register.cs` / `RegisterEmployee.cs`) is **authoritative**; this is **client-side UX only** (live rule
  feedback + a pre-submit guard), never the security boundary.
- **Two Core artifacts, mirroring Android's `:core` factoring:** (a) `PasswordPolicy` — a pure validator in
  `CleansiaCore/Validation` exposing the three individual rule booleans (`hasMinLength`/`hasLetter`/`hasNumber`)
  + an `isValid` aggregate, the parity of the `passwordHas*` getters **lifted out of the VM** (Android left
  them in `RegisterUiState` — iOS fixes that by putting them in Core so partner + customer share one source);
  (b) `PasswordRuleList` — a native-SwiftUI Core component (the parity of the **already-shared** `:core`
  `PasswordRuleList.kt`, which Android moved out of customer-app's SignUpScreen so partner-app reuses it —
  `PasswordRuleList.kt:38-54`, consumed by partner `RegisterScreen.kt` + customer `SignUpScreen.kt:182,200`).
  The component takes `(label, isValid)` rows + a `hasInput` flag (neutral / green-check / red-cross per
  `PasswordRuleList.kt:56-62`) — same three-state row semantics, native SF-Symbol icons (Gate-DP component swap).
- **Reused by customer (T-0312+) without re-deciding.** Customer SignUp (`SignUpScreen.kt`) uses the
  identical password rule today on Android; the iOS customer wave imports the same `PasswordPolicy` +
  `PasswordRuleList` from Core — the harvest pays off immediately at the second call site. **CRC:** new
  `ios-password-policy` role (below).

#### Decision 5 — Record the F1 parity defect: Android partner Register/Forgot VMs HARDCODE English validation strings (iOS does it right)

**RULING: record an Android-behavior-is-wrong parity DEVIATION (iOS does it correctly) + a follow-up note
for the android agent — do NOT replicate the bug.** This is the sanctioned `patterns-mobile.md` **Parity
rule** path ("If the Android behavior is itself wrong, raise a finding — don't silently diverge on iOS only")
and a violation of `consistency.md` **E8** (all user text via `R.string`/`getString`).

- **The defect (verified):** `RegisterViewModel.kt:64-84` and `ForgotPasswordViewModel.kt:45-52` set
  validation error strings as **raw English literals** — `"First name is required"`, `"Please enter a valid
  email"`, `"Password must be at least 8 characters with a letter and a number"`, `"Passwords do not
  match"`, `"You must accept the terms"`, `"Email is required"` — **not** `appContext.getString(R.string.*)`.
  These VMs do **not** inject `@ApplicationContext Context`, so the validation messages **never localize**
  (they render English in all 5 locales). This violates E8 ("All user-facing text via `stringResource`/
  `getString` — already consistent; keep it") — E8's "already consistent" claim is **wrong** for these two
  partner auth VMs.
- **iOS does it right (the deviation):** the iOS Register/Forgot/Confirm VMs use proper
  `Localizable.xcstrings` keys ×5 (en/cs/sk/uk/ru) for every validation message (ADR-0013 D11 / reviewer #10
  i18n completeness — no hardcoded strings). iOS is the **correct** reference here; **do not** copy the
  Android literals. This is the one place T-0305 **intentionally diverges** from Android behavior, and it is
  the *right* divergence (the catalog's Parity rule names exactly this case).
- **Follow-up for the android agent (recorded, not blocking T-0305):** the Android partner
  `RegisterViewModel`/`ForgotPasswordViewModel` should move their validation strings to `R.string.*`
  (inject `@ApplicationContext Context`, mirror how `OrderDetailViewModel.kt:80` already does
  `appContext.getString(...)`), so all 5 locales render. Recorded as a parity-deviation in
  `consistency.md` E8 + this note; the PM files the android fix-ticket from here (small, mechanical i18n fix
  — not part of the iOS wave).

#### New CRC roles (added with the T-0305 wiring)

- **`ios-app-settings-store`** — `AppSettingsStore` (in `CleansiaCore`, UserDefaults-backed):
  *responsibility:* be the **single** device-local (wiped-on-uninstall) preference store — `hasSeenOnboarding`
  (get/`markSeen`) + the resolved 5-locale `language` tag (the `AppSettingsRepository.kt` parity).
  *Collaborators:* `UserDefaults`, `Locale.current` (the language seed). *Does NOT know:* the session/token
  (that is the Keychain `TokenStore`'s — secrets never live here), any business payload, or how a screen
  renders. **If a secret (token, device id) is ever stored here, the responsibility is wrong — it goes in the
  Keychain spine.**
- **`ios-password-policy`** — `PasswordPolicy` (in `CleansiaCore/Validation`): *responsibility:* be the
  **single client-side** password-rule predicate (≥8 && ≥1 letter && ≥1 digit) the partner + customer
  register screens share, the `RegisterViewModel.kt:37-39` parity lifted to Core. *Collaborators:* the
  `PasswordRuleList` component (renders its rule booleans), the register/sign-up VMs. *Does NOT know:* it is
  the **authoritative** check (it is not — the backend `BaseAuthValidator` is); any UI styling; the network.

---

### 7.6 T-0306 (Phase-2 map seam + MapKit + partner AddressPicker) — acceptance scope + the four Understand-pass rulings (recorded 2026-06-26, architect)

T-0306 ships the **first concrete shape of the ADR-0013 D6 / ADR-0014 D6′ map seam**: (a) the Core
`MapProvider`/`GeocodingService` protocols + the `Coordinate`/`GeocodedAddress` value types, (b)
`MapKitMapProvider` + `CLGeocoderGeocodingService`, (c) the partner `AddressPickerView`/`VM` that returns a
`GeocodedAddress` via callback (it does **NOT** wire into the AddressSection — that is **T-0310**). The
Understand pass surfaced four decisions; **all four APPLY accepted ADRs — no new ADR** (the §7.2/§7.4/§7.5
"record, not ADR" precedent: the trade-offs are owned by ADR-0013 D6, ADR-0014 D6′, and ADR-0018 Gate-DP,
plus the analogous defer-the-affordance call §7.2/§7.4 already made). **Android parity source (the iOS port
mirrors it):** `partner-app/.../features/profile/AddressPickerScreen.kt` (full-bleed map + a STATIC center-pin
overlay the map pans under + search + reverse-geocode-on-idle 500ms + forward-search 300ms + a confirm card)
+ `AddressPickerViewModel.kt` (a thin DI seam, no state of its own) + `core/.../location/{ReverseGeocoding
Service,GeocodedAddress,LocationService,MapStyles}.kt`. **Gate-DP applies** (T-0306 is a screen ticket): the
picker cites `AddressPickerScreen.kt`; native SwiftUI; iOS-wins-on-conflict + the one noted divergence (D2).

**IN — T-0306 acceptance scope:**
- **Core seam:** `MapProvider` (picker-map factory) + `GeocodingService` protocols + `Coordinate` +
  `GeocodedAddress` value types in `CleansiaCore/Location`; **`MapKitMapProvider`** + **`CLGeocoderGeocoding
  Service`** as the default impls (the **only** sanctioned MapKit/CoreLocation consumers — feature/VM import
  neither, reviewer #7/#27).
- **Partner `AddressPickerView`/`VM`:** full-bleed picker map (iOS-16 `Map(coordinateRegion:interactionModes:
  showsUserLocation:annotationItems:[])` + a SwiftUI overlay **center pin** the map pans under — NO
  `Map{Marker}`/`onMapCameraChange`, reviewer #12) + search field + dropdown (forward-geocode) + the confirm
  card; **returns `GeocodedAddress` via `onConfirmed` callback**. The VM owns the reverse-geocode-on-idle
  debounce (Combine/`Task`, since iOS-16 has no idle callback) and the forward-search debounce.
- **Geocoding contract:** best-effort (nil/`[]` on error, cancel-before-refire); **300ms forward / 500ms
  reverse** ported VERBATIM from Android.
- **Centers on the Prague default** (`14.4378, 50.0755`, zoom 15 — the `AddressPickerScreen.kt:90-91` parity);
  **pan-to-place + search at full parity** (both usable with no location fix).

**DEFERRED — explicitly out of T-0306, with the ticket each lands in:**
- **Current-location auto-center + the my-location FAB + the `LocationProvider`/`CLLocationManager` seam** →
  **T-0310** (picker→AddressSection wiring), gated on **T-0325**'s `NSLocationWhenInUseUsageDescription` plist
  key (owner). **The recorded Gate-DP divergence** (Decision 2 below).
- **The full-bleed `OrderDetail` map + the 3-snap sheet + the service-area polygon overlay** (the `MKMapView`/
  `UIViewRepresentable` surface, ADR-0014 D6′) → **T-0307**, added as an **additive `MapProvider` method**
  (Decision 1 below) — not designed ahead.
- **Wiring the picker into the AddressSection + persisting via `UpdateAddressInfo`** → **T-0310** (the picker
  hands `GeocodedAddress` back via callback; it persists nothing itself — the `AddressPickerScreen.kt:96-101`
  parity).
- **A custom Mapbox-Studio map style** (`MapStyles.kt`) → NOT ported (Decision 4; Q-IOS-02 stays "No").

#### Decision 1 — `MapProvider` protocol shape: a MINIMAL picker factory NOW, T-0307's full-bleed surface added ADDITIVELY later (the one canonical seam)

**RULING: minimal picker factory now; the full-bleed/overlay surface is an additive method later — record as
"the one way iOS does maps behind the seam". An application of ADR-0013 D6's `MapProvider` seam — no new ADR.**

- **What ships now:** `MapProvider` exposes only the **picker-map factory** T-0306 needs — a producer of a
  SwiftUI view bound to a region + the static center-pin overlay (iOS-16 variant). The exact Swift surface is
  the dev's (sketch: a `func pickerMap(region:showsUserLocation:) -> some View`/`AnyView`); the **contract** is
  what's fixed: the feature gets a region-bound picker view, never a vendor type.
- **What's added LATER, additively:** T-0307's full-bleed `OrderDetail` map + 3-snap sheet + service-area
  polygon overlay is a **new additive method** on the same protocol (e.g. `fullBleedMap(region:overlays:
  annotations:)`), implemented `MKMapView`-via-`UIViewRepresentable` inside `MapKitMapProvider` (ADR-0014 D6′).
  Additive methods don't break the picker call site, so deferring is **free** — vs designing the rich surface
  now, which would guess T-0307's camera/overlay/gesture needs before they exist (a speculative shape that gets
  rewritten). The alternative — **a richer designed-ahead `MapProvider`** — was **rejected**: it front-loads
  T-0307/0310/0314's unknown needs into T-0306, and the seam's whole point (ADR-0013 D6) is that the
  *contract*, not the *surface area*, is what protects features.
- **`GeocodingService` is a clean 1:1 port of `ReverseGeocodingService.kt`** (`reverseGeocode(lat,lng) →
  GeocodedAddress?`, `forwardGeocode(query,countryIsoCodes) → [GeocodedAddress]`) **minus the Mapbox token +
  the OkHttp/network args** — under MapKit it is `CLGeocoder`/`MKLocalSearch`, no token, no HTTP client.
  `GeocodedAddress` mirrors the Kotlin fields (lat/lng/street/city/zipCode/country/countryIsoCode/formatted).
- **The one sanctioned consumer:** the `MapKitMapProvider`-produced view + `CLGeocoderGeocodingService` are the
  **only** code importing MapKit/CoreLocation; **feature/VM code imports neither** (reviewer #7 + #27).
- Reused by **T-0307/T-0310/T-0314** by adding the next additive method, not re-deciding the seam.

#### Decision 2 — current-location / permission: DEFERRED out of T-0306 (the one flow-touching Gate-DP divergence — architect sign-off)

**RULING: DEFER current-location + the my-location FAB + the `LocationProvider` seam out of T-0306; ship
pan+search parity centered on the Prague default; home them to T-0310 (gated on T-0325). This is the recorded
Gate-DP divergence with architect sign-off — not a new trade-off** (the same defer-the-affordance-whose-
dependency-isn't-live call §7.2 and §7.4 already made; ADR-0018 D3 component/affordance divergence).

- **What Android does:** the picker auto-centers on the FusedLocation fix on open + shows a my-location FAB
  (`AddressPickerScreen.kt:131-161` auto-center / permission launch, `:272-295` the FAB; via `LocationService.kt`).
- **Why defer:** the iOS prompt needs `NSLocationWhenInUseUsageDescription` in the app `Info.plist`, which is
  **owner ticket T-0325** (purpose strings). **Without that key the iOS permission prompt never appears** — so
  a my-location FAB built in T-0306 would be a **dead control**. The picker is **fully usable without it**:
  pan-to-place + search both work and reach full parity; it just **centers on the Prague default**
  (`14.4378, 50.0755` — the `AddressPickerScreen.kt:90-91` parity) instead of the device fix.
- **Home:** T-0310 (when the picker is wired into the AddressSection and T-0325's plist key exists) introduces
  the `LocationProvider` seam (`CLLocationManager`, behind a Core protocol like `MapProvider`/`GeocodingService`
  so the feature never imports CoreLocation) + the my-location FAB.
- **The recorded Gate-DP divergence (architect sign-off):** *"iOS picker omits current-location pending T-0325;
  pan/search parity is full; the divergence touches a deferred affordance, not layout/flow/branding."* This is
  an ADR-0018 D3-shaped divergence (a component/affordance gap, **never** a layout/flow/branding change) — it
  passes Gate-DP assertion #3 (noted in-ticket, touches only the affordance). **The alternative — parity now —**
  would grow T-0306 by the `LocationProvider` seam **and** add a hard `manual_step:
  NSLocationWhenInUseUsageDescription (T-0325, owner)` dependency onto a seam ticket; **rejected** — it couples
  the map-seam delivery to an owner plist step for an affordance the picker doesn't need to be usable.

#### Decision 3 — geocoding error/throttle + the NO-`UiState` ruling (reviewer note #27)

**RULING: CONFIRMED as briefed — best-effort geocoding + verbatim debounce + NO sealed state on the picker.
A confirmation of the Android behavior (ADR-0013 "mirror the code") + the catalog's E1/E2-scoping rule — no
new ADR.**

- **(a) `CLGeocoder` is best-effort.** Cancel the in-flight geocode before re-firing (`kCLErrorGeocodeCanceled`
  is expected and swallowed); return `nil`/`[]` on any error; **never block the confirm or crash** — the
  `runCatching{}.getOrNull()` / `.getOrDefault(emptyList())` parity (`ReverseGeocodingService.kt:41,79`). A
  failed reverse-geocode leaves `resolved == nil` (the confirm button stays disabled — `AddressPickerScreen.kt:374`
  parity), a failed forward-search shows the empty-results row.
- **(b) Debounce timings port VERBATIM.** **300ms forward** (`AddressPickerScreen.kt:188`), **500ms
  reverse-on-idle** (`:171`). They double as Apple's `CLGeocoder` rate-limit guard. **iOS-16 has no map idle
  callback**, so reverse-geocode-on-idle is a **VM-owned Combine/`Task` debounce** off the region binding (the
  VM observes the region and debounces 500ms before reverse-geocoding) — not a map delegate callback.
- **(c) The AddressPicker correctly has NO `UiState<T>`/`ActionState`.** It is an **interactive map** with
  plain `@Published` state (`resolved: GeocodedAddress?`, `lookingUp`, `searchQuery`, `searchResults`,
  `searching` — the `remember{mutableStateOf}` set, `AddressPickerScreen.kt:117-122`) + a **one-shot confirm
  event** (`onConfirmed(GeocodedAddress)`, a callback, not a mutation result). It is **neither** an E1
  load-fetch screen (no `loading/error/loaded` fetch lifecycle) **nor** an E2 mutation screen (no
  `idle/submitting/error` submit). So the sealed-state archetypes are **correctly scoped OUT** — and a reviewer
  must **NOT** flag the absence of `UiState`/`ActionState` here as a consistency gap (it would be wrong to add
  them). **Reviewer note #27** records this so the scoping is intentional, not an oversight.

#### Decision 4 — NO Mapbox token / security (net reduction in secret-management surface vs Android)

**RULING: CONFIRMED — zero map token, zero map SDK, zero `Package.swift` change; a net REDUCTION in secret
surface vs Android. Q-IOS-02 stays defaulted "No". Recorded as the no-token security note — no new ADR**
(it confirms ADR-0013 D6's "MapKit = free, native, no token" and Q-IOS-02's default).

- **Under the MapKit default there is ZERO map token, ZERO map SDK dependency, ZERO `Package.swift` change** —
  **MapKit + CoreLocation are system frameworks** (no SPM entry, no binary added).
- **Contrast Android:** `ReverseGeocodingService.kt:21` takes a `MAPBOX_ACCESS_TOKEN` (fed from BuildConfig by
  the Hilt location module); the token is a rotated secret + a leak surface. **iOS removes it entirely** — one
  fewer secret to provision/rotate. **No owner provisioning step for maps** (the §7 owner-steps row already
  reads "Mapbox token ONLY IF Q-IOS-02 flips the default — not needed under MapKit").
- **Q-IOS-02 (Mapbox-brand) stays defaulted "No"** (MapKit standard style). `MapStyles.kt`'s custom Mapbox
  Studio style is **NOT ported** — the stock MapKit style is the parity baseline; a hard brand requirement is
  the only input that flips it, behind the **unchanged** `MapProvider` seam (a provider swap, not an
  architecture change). The Q-IOS-02 record in `questions/open.md` is unchanged (still "No / default").

#### New CRC role (added with the T-0306 wiring)

- **`ios-geocoding-service`** — `GeocodingService` (protocol) + `CLGeocoderGeocodingService` (default impl, in
  `CleansiaCore/Location`): *responsibility:* forward/reverse geocode text↔`GeocodedAddress`, **best-effort**
  (nil/`[]` on error, cancel-before-refire), with **no token**. *Collaborators:* `CLGeocoder`/`MKLocalSearch`,
  the `Coordinate`/`GeocodedAddress` value types. *Does NOT know:* the Mapbox token (there isn't one), the
  network client (it's a system framework), which feature/VM consumes it, or how the picker renders. (The
  ADR-0013 `ios-map-provider` CRC is unchanged — `MapKitMapProvider` is its default impl; T-0306 adds the
  picker factory, T-0307 adds the full-bleed method.)

---

### 7.7 T-0310 (Phase-2 partner Profile tab + section editors + onboarding chain + Devices + Preferences) — acceptance scope + the five Understand-pass rulings (recorded 2026-06-26, architect)

T-0310 fills the partner **Profile tab** (replacing the `PlaceholderTab` in `PartnerShellView.swift:36`) with the
**hub** (hero + contract-status chip + section-group rows + logout) + **6 section editors** (Personal / Address /
Identification / Bank / Emergency / Documents) over a new `PartnerProfileClient`
(`PartnerEmployeeAPI`/`PartnerCountryAPI`, generated + present, riding the **ADR-0019** spine), the **onboarding
chain**, **Devices** (Device/Mine list + revoke — **SECURITY-owned**, decisions 6–8, ruled separately), and
**Preferences** (Language / Theme). The Understand pass surfaced five architecture/scope decisions (D1–D5) +
two scope items (A current-location / B notifications). **All five APPLY accepted ADRs or confirm Android parity —
NO new ADR** (the §7.2/§7.4/§7.5/§7.6 "record, not ADR" precedent: ADR-0020 owns the navigation trade-off,
sprint-12 §7.5 D1 owns the settings store, §7.6 D2 owns the defer-the-affordance call, ADR-0018 Gate-DP owns the
divergence form, and the E1 divergence applies the `patterns-mobile` Parity rule). **Android parity source (the iOS
port mirrors it):** `partner-app/.../features/profile/` — `ProfileScreen.kt` + `ProfileViewModel.kt` (the hub),
`PersonalSectionScreen.kt`/`…ViewModel.kt` + the 5 sibling `*Section*` files (the editors), `OnboardingChainHeader.kt`
+ `SectionScaffold.kt` (the chain chrome), `AddressSectionScreen.kt`/`…ViewModel.kt` (the AddressPicker wiring);
`features/orders/RegistrationLockViewModel.kt` + `OnboardingChainViewModel.kt` (the lock→chain routing);
`core/settings/AppSettingsRepository.kt` (Preferences). The flat `PartnerNavHost.kt` is the navigation source;
`navigation/NavRoutes.kt` is the typed-route inventory. **Gate-DP applies** (T-0310 is a screen ticket): every
screen cites its Android Compose counterpart; native SwiftUI; iOS-wins-on-conflict + the noted divergences (D5, A).

> **SECURITY owns decisions 6–8 (the device-id / revoke gate) — out of scope here.** This §7.7 rules only D1–D5
> + scope A/B (navigation, lock routing, service-area scope, settings store, the E1 divergence, current-location,
> notifications). The Device/Mine list + revoke contract, the `X-Device-Id`-match invariant on revoke, and the
> "revoke-this-device" gate are ruled in parallel by the **security** charter; this record does not touch them.

#### Decision 1 — Profile nav structure: an in-tab `NavigationStack` over a typed `ProfileRoute` enum (CONFIRMED — ADR-0020 application, no new ADR)

**RULING: CONFIRM the iOS parity. The Profile tab hosts its OWN `NavigationStack` driven by a typed `ProfileRoute`
enum, INSIDE the `.dashboard` shell's Profile tab. This is the direct application of ADR-0020 D2's split-scope —
the root-`enum` switch (`PartnerRootView`) stays the AUDIENCE selector; `NavigationStack` + a typed route is the
INTRA-audience push container. No new ADR (ADR-0020 already canonicalized the split; reviewer #23).**

- **Why this is settled, not a new decision.** ADR-0020 D2 (`ios-app-architecture.md` §"Partner router shape" +
  `patterns-mobile` `navigation.Routes` split rows) already fixed: *top-level audience = root-`enum`; intra-audience
  push = `NavigationStack` + typed route enum (OrderDetail, **ProfileSection**, onboarding-chain sections explicitly
  named there)*. The Profile tab's section pushes are exactly the "intra-audience push" the rule names. Android uses
  **one flat `NavHost`** (`PartnerNavHost.kt`) because Compose Navigation has no nested-host idiom in play here; iOS
  mirrors the **decision tree, not the mechanism** (ADR-0013 D6 / ADR-0020 D5) — a per-tab `NavigationStack` is the
  native iOS shape for tab-local push (each tab owns its own back-stack; switching tabs preserves each stack).
- **The `ProfileRoute` shape** (derived 1:1 from `NavRoutes.kt:54-91`, minus the audience-level cases the root enum
  owns — `Splash/Login/RegistrationLock/Main` are NOT `ProfileRoute`s):

  ```swift
  // Intra-Profile-tab push routes. Hosted by the Profile tab's NavigationStack (NOT the audience root).
  enum ProfileRoute: Hashable {
      case personal(onboarding: Bool = false)         // NavRoutes ProfilePersonal(onboarding)
      case address(onboarding: Bool = false)          // ProfileAddress(onboarding) — wires the T-0306 AddressPicker
      case identification(onboarding: Bool = false)   // ProfileIdentification(onboarding)
      case bank(onboarding: Bool = false)             // ProfileBank(onboarding)
      case emergency                                  // ProfileEmergency (not a gate field → no onboarding flag)
      case documents                                  // ProfileDocuments (not a gate field → no onboarding flag)
      case preferenceLanguage                         // PreferenceLanguage
      case preferenceTheme                            // PreferenceTheme
      case devices                                    // Devices  (the screen is SECURITY-ruled; the ROUTE lives here)
  }
  ```

  - **The `onboarding: Bool` associated value is load-bearing and ported verbatim** (`NavRoutes.kt:64-67`): the four
    **gate sections** (Personal/Address/Identification/Bank) carry it; on save, `onboarding == true` chains forward to
    the next missing section, `false` pops back (maintenance edit). Emergency + Documents are NOT gate fields, so they
    carry **no** flag (matching `NavRoutes.kt:69-70`). This is the same "a flat-enum route case may carry a payload"
    pattern ADR-0020 fold-in (sprint-12 §7.5 D2) already sanctioned for `.verifyEmail(email:)` — applied to an
    intra-audience route here. The route still only *lands* the destination; the section VM interprets the flag.
  - **`AddressPicker` is NOT a `ProfileRoute` case.** The picker (T-0306) is presented by the Address section as a
    `.sheet`/`.fullScreenCover` returning `GeocodedAddress` via its `onConfirmed` callback (the
    `AddressPickerScreen.kt:96-101` / `NavRoutes.kt:72-80` "writes into the previous back-stack entry + pops itself"
    parity, done iOS-natively via the callback rather than a `SavedStateHandle` result key). It is a modal return-value
    flow, not a tab-push — so it is not in the back-stack enum.
- **Reviewer angle (#28a, below):** the Profile tab is an in-tab `NavigationStack` over `ProfileRoute`; audience-level
  states (`Splash/Login/Lock/Main`) are **never** `ProfileRoute` cases (they belong to the `PartnerRootView` root enum).
  Modeling a section push as an audience hop, or the audience as a `ProfileRoute`, is a finding (the ADR-0020 #23 split).

#### Decision 2 — RegistrationLock "Fix"-CTA routing: the lock owns its OWN local `NavigationStack` + onboarding-chain VM and pushes the SHARED section screens over itself (CONFIRMED — Android parity, fail-closed; no new ADR)

**RULING: lock-owns-its-own-stack. The `RegistrationLock` (a ROOT audience state — `PartnerRootView` `.registrationLock`,
NOT in the shell) hosts its OWN local `NavigationStack` + the onboarding-chain VM, and pushes the SAME section
Views/VMs the Profile tab uses OVER itself. On pop, the lock re-resolves its status and `onCompleted` fires. NO
cross-audience routing into the shell's Profile tab. This is the load-bearing structural call — and it is a
confirmation of the Android gate (ADR-0013 "mirror the code"), fail-closed (#24 preserved). No new ADR.**

This is the genuinely structural decision in T-0310, so it is grounded in source line-by-line:

- **Android pushes the section screens OVER the lock, in the SAME flat `NavHost`, WITHOUT unlocking the shell.**
  `OnboardingChainViewModel.advanceOrFinish` (`OnboardingChainViewModel.kt:86-121`) navigates section→section and, on
  finish, `navController.popBackStack()` / `navigate(next){ popUpTo(NavRoute.RegistrationLock){ inclusive = false } }`
  — i.e. **the lock stays UNDERNEATH** (`inclusive = false`), and the lock re-resolves via its own `ON_RESUME`
  effect (`RegistrationLockViewModel.kt:134-136` `onResume()` → `ensureFreshOrCachedAsync()`). The gate is
  **reachable-from-lock but the shell stays unreachable** until `isRegistrationComplete()` flips
  (`RegistrationLockViewModel.kt:103-109`). **The sections are reached from the lock, NOT through the shell's Profile
  tab.**
- **iOS mirrors the tree, not the single-NavHost mechanism (ADR-0020 D5).** Because iOS models the audience with the
  **root `enum`** (not one flat NavHost), `.registrationLock` is a **distinct root case** that **owns its own local
  `NavigationStack`** (over the same `ProfileRoute` cases — the gate sections) + its own onboarding-chain VM. The lock
  pushes section screens onto ITS stack; on pop, the lock VM re-resolves (the `onResume`/`ensureFreshOrCached` parity),
  and when complete it flips the root `enum` to `.dashboard` (`SplashGate` re-resolution — the existing
  `onCompleted`/watermark path, §7.4 Decision 1, byte-unchanged). **The shell's Profile-tab stack is never entered
  from the lock** — no cross-audience routing.
- **HOW THE SECTION SCREENS ARE SHARED (the explicit decision asked for): ONE set of section Views + VMs, TWO hosts.**
  The Personal/Address/Identification/Bank section `View`s and `…SectionViewModel`s are written **once** (mirroring the
  single Android `PersonalSectionScreen.kt` &c., which both the lock chain and the Profile menu reach — the Android
  proof that they are one set: `RegistrationLockViewModel.firstMissingProfileSection(forOnboarding=true)` and the
  Profile hub's `onNavigateToPersonal` both resolve to the **same** `NavRoute.ProfilePersonal`, `NavRoutes.kt:64`).
  iOS hosts that one set from **two `NavigationStack`s**: (1) the **Profile tab's** stack (maintenance edits,
  `onboarding == false` → pop on save) and (2) the **lock's** stack (onboarding chain, `onboarding == true` → chain
  forward on save). The `onboarding: Bool` on the `ProfileRoute` case (D1) is the **single switch** that picks
  pop-back vs chain-forward — exactly the Android flag (`NavRoutes.kt:55-67`). **A second, forked copy of any section
  View/VM for the lock is a finding** — the whole point of the `onboarding` flag is one section set, two entry contexts.
- **Why lock-owns-its-own-stack beats cross-audience routing into the shell's Profile tab (the rejected alternative):**
  routing the Fix CTA into the shell's Profile tab would require **rendering the shell** (the audience the gate exists
  to keep unreachable) — a fail-**OPEN** hole: an incomplete partner would reach the shell's tab bar to get to a
  section. The Android gate is explicitly fail-closed (`isRegistrationComplete()` AND-predicate; both error paths
  preserve-lock, `RegistrationLockViewModel.kt:197-211`) and the §7.4/#24 ruling forbids any shell reach before
  complete. Cross-audience routing **breaks #24**; lock-owns-its-own-stack keeps the shell unreachable and matches
  Android's `popUpTo(RegistrationLock){inclusive=false}` exactly. **Rejected: cross-audience routing into the shell's
  Profile tab** (fail-open; violates #24 + ADR-0020's replace-semantics audience switch).
- **T-0304 deferral discharged.** T-0304 shipped the lock "Fix" CTAs **inert** (`StepRow` rendered a chevron, no
  `onFix`, no VM routing — §7.4 Decision 3). T-0310 wires them: the `onFix` callback pushes the resolved section
  (`firstMissingProfileSection`-parity) onto the lock's stack with `onboarding == true`. The `contact_support`
  affordance on the Rejected row (carried inert since T-0304, `registration_lock_action_contact_support`) is wired
  here too (the Android `mailto:`-in-row, `RegistrationLockViewModel.kt:249-255`).
- **Reviewer angle (#28b, below):** the lock owns its own `NavigationStack` + chain VM and pushes the shared section
  set with `onboarding == true`; on pop it re-resolves and only the success watermark flips the root to `.dashboard`;
  **no Fix CTA renders or routes into the shell's Profile-tab stack** (a shell reach before complete is a fail-open
  finding — composes with #24).

#### Decision 3 — ServiceAreaProvider scope: DEFER the `ServiceAreaRow` to a follow-up; ship the Address section without the live indicator in T-0310 (a recorded Gate-DP divergence)

**RULING: DEFER. Port the AddressSection's pan/search/save at full parity in T-0310 (wiring the T-0306 AddressPicker),
but DEFER the live 3-state `ServiceAreaRow` advisory indicator + the `ServiceAreaProvider` Core seam to a follow-up
(T-0334). The indicator is ADVISORY-ONLY (never a save gate), so the Address section is fully usable without it —
this is the same defer-the-advisory-affordance call §7.6 D2 made for current-location. Recorded as a Gate-DP
divergence with architect sign-off. No new ADR.**

- **What the row is (and is not).** `AddressSectionViewModel.kt:50-55` defines a 3-state `ServiceAreaStatus`
  (`InServicedCity` / `OutsideServicedCity` / `CountryNotServiced` / `Unknown`), refreshed fire-and-forget
  (`:163-195`) from `core/servicearea/ServiceAreaProvider` (`ServiceAreaProvider.kt`). It is **explicitly advisory**:
  the source comment says it *"only feeds the indicator row — failures degrade to Unknown rather than blocking save"*
  (`:165-167`), and a cleaner home address is *"aren't blocked by city — only customer order creation is"*
  (`:42-44`). The **save** path resolves `countryId` independently (`:213-222`) and does not read the row. So the row
  is a *nice-to-have heads-up*, not a correctness gate.
- **Why DEFER is right (scope, not under-delivery).** Porting the row is **real additional scope**, not a one-liner:
  it requires the `:core` `ServiceAreaProvider` (lazy-cached countries+cities, a `Mutex`, `refresh()` on sign-in),
  the per-app `ServiceAreaDataSource` binding seam (a new `:core` interface each app `@Binds` to its generated client
  — `ServiceAreaProvider.kt:25-31` + `AddressSectionViewModel.kt:88`), `ServicedCity`/`ServicedCountry` value types,
  and an ISO alpha-2↔alpha-3 reconciliation (`AddressSectionViewModel.kt:178-181`). That is a **Core seam port in its
  own right** — and the **same provider also backs the forward-geocode country-bias** (`ServiceAreaProvider.kt:14-16`,
  `servicedCountryIsoCodes()`), which T-0306 **also deferred** (it shipped unbiased MapKit search). Pulling the seam
  into T-0310 would balloon it; the picker's pan/search/save all work without it.
- **The recorded Gate-DP divergence (architect sign-off):** *"iOS T-0310 Address section ships pan/search/save at full
  parity; the advisory `ServiceAreaRow` (3-state serviced-city indicator) is deferred to T-0334. The divergence
  touches a deferred ADVISORY affordance, not layout/flow/branding, and never a save gate."* This passes Gate-DP
  assertion AR-DP-3 (noted in-ticket, touches only the affordance) — identical in shape to §7.6 D2's current-location
  divergence. **Rejected: port it now** — it front-loads the `ServiceAreaProvider` Core seam (+ its country-bias use,
  itself deferred from T-0306) into a screen ticket for an advisory row, growing T-0310 past `M`.
- **Follow-up filed:** **T-0334** (port `ServiceAreaProvider` Core seam + the `ServiceAreaRow` advisory indicator +
  the forward-geocode country-bias it also backs) — `draft`, suggested home after T-0310.

#### Decision 4 — `AppSettingsStore` extension: EXTEND the one general store with writable language + a theme enum + setters; HONOR theme app-wide via `.preferredColorScheme` in T-0310 (CONFIRMED — sprint-12 §7.5 D1 application, no new ADR)

**RULING: EXTEND the single general `AppSettingsStore` (NOT a second store) with a writable language (`setLanguage`)
+ a `Theme` enum (System/Light/Dark) + `setTheme`; and HONOR the theme app-wide via `.preferredColorScheme` on the
root in T-0310 (a small additive root wiring). This is the direct application of sprint-12 §7.5 Decision 1 ("one
general store") — the store was deliberately built general so T-0307+/Preferences add a property here, not a new
store. No new ADR.**

- **Why extend, not add a store.** sprint-12 §7.5 D1 (reviewer #26a) already ruled the **one** way to do device-local
  settings: a single general `AppSettingsStore` in `CleansiaCore`, `UserDefaults`-backed (the `AppSettingsRepository.kt`
  DataStore parity — `partner_app_settings`), *"so T-0307+ / the customer wave add a property here instead of standing
  up a new store each time."* Android's `AppSettingsRepository.kt:22-43` holds **all three** keys in **one** DataStore
  (`theme`, `language`, `onboarding_seen`) with `setTheme`/`setLanguage` setters — iOS extends its one store to match.
  A second settings store is **explicitly** a reviewer-#26a finding.
- **The extension (the `AppSettingsRepository.kt:37-51` parity).** Add to `AppSettingsStore`: `setLanguage(tag)` making
  the existing resolved language tag **writable** (persist the chosen tag ∈ `{en,cs,sk,uk,ru}` or a `System` sentinel
  → the resolver already handles persisted-if-in-set → `Locale.current` → `"en"`, §7.5 D1); a `Theme` enum
  (`.system`/`.light`/`.dark` — the `ThemePreference` parity, `ProfileScreen.kt:497-503`) with a getter + `setTheme`.
  Both remain non-secret + wiped-on-uninstall (`UserDefaults`, NOT Keychain — #26a holds).
- **Honor theme NOW (the small additive call).** Wire `.preferredColorScheme(appSettings.theme.colorScheme)` on the
  partner app root (`.system` → `nil` = follow device, `.light` → `.light`, `.dark` → `.dark`) — the
  `MainActivity`-collects-and-propagates parity (`AppSettingsRepository.kt:17-19` "Collected once in MainActivity and
  propagated"). It is **additive root wiring** (one modifier reading one published value), not an architecture change,
  so honoring it in T-0310 avoids shipping a Preferences row that visibly does nothing (a "dead control" of the §7.6
  kind). **Language honoring** follows the existing `Localizable.xcstrings` + bundle-override mechanism the app already
  uses; the Preferences row writes the tag, the app applies it on next resolution (Android applies on relaunch via the
  same DataStore read — iOS parity is to apply the locale override the same way the app already resolves it).
- **Reviewer angle (#28c, below):** language + theme read/write the **one** `AppSettingsStore` (UserDefaults, not
  Keychain); no second settings store; the theme is honored via `.preferredColorScheme` on the root (no dead
  Preferences row). A separate `ThemeStore`/`LanguageStore`, or either pref in the Keychain, is a finding (#26a sibling).

#### Decision 5 — UiState divergence from Android flag-bags: iOS is born sealed-state canonical (RECORD-THE-FINDING — iOS-right divergence, NOT a parity bug; Android E1 fix filed)

**RULING: RECORD as an intentional iOS-right divergence — NOT a parity bug. Android's `ProfileUiState` + the section
`*UiState` are flag-bags (E1 violation, already noted in `consistency.md` E1 + `patterns-mobile`). iOS is born
sealed-state canonical: load-fetch screens (the hub + each section's initial load) use `UiState<T>`
(`.loading`/`.error`/`.loaded`); the save action uses `ActionState` (`.idle`/`.submitting`/`.error`). This is the
canonical application of the `patterns-mobile` Parity rule ("Android is wrong → diverge correctly on iOS, raise the
Android finding, don't copy"). The Android E1 fix is filed as T-0337. No new ADR.**

- **The flag-bags (confirmed in source).** `ProfileViewModel.kt:26-36` `ProfileUiState(isLoading, employee?,
  contractStatus?, error?, isSignedOut)` and `PersonalSectionViewModel.kt:17-30` `PersonalSectionUiState(isLoading,
  isSaving, …field strings…, firstNameError?, lastNameError?, error?, isSaved)` are **single flag-bag `data class`es**
  — exactly the **E1** smell (`consistency.md:160-163`: *"never a single flag-bag `data class` with
  `isLoading`/`error`/`isXSuccessful` booleans (which permits impossible states)"*; partner is named as "mostly
  wrong"). They mix a **load** lifecycle (`isLoading`/`employee`/`error`) and a **save** lifecycle
  (`isSaving`/`isSaved`) in one bag, permitting impossible states (`isLoading && isSaved`).
- **The iOS-canonical shape.** Per ADR-0014 D2′ + `consistency.md` E1/E2 + `patterns-mobile`: the **hub** and each
  **section's data load** are E1 load-fetch → sealed `UiState<T>` (`.loading`/`.error(canRetry:)`/`.loaded(T)`); the
  **section save** is an E2 mutation → `ActionState` (`.idle`/`.submitting`/`.error`) + a one-shot success effect
  (the "saved → pop / chain forward" effect, not an `isSaved` flag). This is **not** the picker case (§7.6 D3, which
  is correctly stateless) — section editors DO have both a load and a mutation lifecycle, so both archetypes apply.
- **Why record (not silently diverge, not copy).** The `patterns-mobile` Parity rule + the F1 precedent (§7.5 D5):
  *"If the Android behavior is itself wrong, raise a finding — don't silently diverge on iOS only."* So: iOS does it
  right, AND the Android E1 flag-bag fix is filed as a PM follow-up (**T-0337**), independent of the iOS wave (same
  shape as F1/T-0333). Note the section VMs **also** hardcode English validation strings (`PersonalSectionViewModel.kt:82`
  "First name is required", `:91` "Profile not loaded yet"; `AddressSectionViewModel.kt:201,205,220`) — the **same
  F1/E8 class** as Register/Forgot (§7.5 D5 / T-0333). iOS localizes these ×5 (reviewer #10); **T-0337 folds the
  section-VM string-literal fix in with the E1 flag-bag fix** (one android profile-VM cleanup ticket).
- **Reviewer angle (#28d, below):** the Profile hub + section loads are sealed `UiState<T>`; saves are `ActionState`
  + a one-shot effect; **no flag-bag `…UiState` struct** (the Android shape) and **no hardcoded validation/error
  strings** (every message an `.xcstrings` key ×5). A ported flag-bag or a hardcoded literal is a finding.

#### Scope A — Current-location (the T-0306-deferred my-location FAB + `LocationProvider` Core seam): DEFER entirely to a follow-up gated on T-0325; record the `LocationProvider` protocol shape regardless

**RULING: DEFER the current-location FAB + the `LocationProvider` seam entirely to a follow-up (T-0335) gated on
T-0325. Ship T-0310's Address section with the AddressPicker pan/search wiring only (full T-0306 parity, Prague
default center). Record the `LocationProvider` protocol shape now (the Core-seam design for whenever it lands). This
is the §7.6 D2 ruling reaching its homed ticket — T-0306 explicitly homed current-location to "T-0310 IF T-0325's
plist key exists" — and T-0325 is STILL an open `proposed` owner manual_step, so building the FAB now ships a dead
control. No new ADR.**

- **T-0325 status check (the decisive input, verified).** T-0325 (`NSLocationWhenInUseUsageDescription` + the purpose
  strings / entitlements) is **`proposed`** in the §10.2 / §3 tables and the INDEX — i.e. the plist key is **NOT
  landed**; it remains an open **owner manual_step** (it has no committed Info.plist/`project.yml` entry). Per §7.6
  Decision 2 + `patterns-mobile` ("building the my-location FAB before T-0325's plist key exists … a dead control"
  is a reviewer-rejected finding) + the §7.6/T-0331 dead-control precedent: **without the key the iOS permission
  prompt never appears**, so the FAB would silently no-op.
- **What ships in T-0310 (full picker parity, no FAB):** the AddressPicker wired into the Address section
  (pan-to-place + search), centered on the **Prague default** (`14.4378, 50.0755`, zoom 15 — the
  `AddressPickerScreen.kt:90-91` parity), returning `GeocodedAddress` via `onConfirmed`. Both pan and search are fully
  usable with no location fix — the picker reaches **full T-0306 parity** without current-location.
- **The `LocationProvider` protocol shape (recorded now — the Core seam design, NOT built in T-0310):** homed in
  `CleansiaCore/Location` behind a protocol (so feature/VM code never imports CoreLocation — the #7/#27 seam rule),
  the `LocationService.kt` parity:

  ```swift
  // CleansiaCore/Location — the one-shot current-location seam (NOT built in T-0310; lands in T-0335 gated on T-0325).
  protocol LocationProvider {
      // Permission state without importing CoreLocation at the call site.
      var authorizationStatus: LocationAuthorizationStatus { get }          // notDetermined/denied/restricted/authorized
      func requestWhenInUseAuthorization() async -> LocationAuthorizationStatus
      // One-shot fix for "center the picker on me" — best-effort, nil on denied/unavailable (never throws to the FAB).
      func currentLocation() async -> Coordinate?
  }
  // Default impl: CLLocationManagerLocationProvider (the ONLY CoreLocation consumer besides the providers).
  enum LocationAuthorizationStatus { case notDetermined, denied, restricted, authorized }
  ```

  Recording the shape now (without building it) costs nothing and means T-0335 slots the FAB in additively — the same
  "design the seam, defer the affordance" move §7.6 D1/D2 made for `MapProvider`'s full-bleed method.
- **The recorded Gate-DP divergence (architect sign-off, carried from §7.6 D2 to its homed ticket):** *"iOS T-0310
  Address section omits the my-location FAB pending T-0325; pan/search parity is full; the divergence touches a
  deferred affordance, not layout/flow/branding."*
- **Rejected: build the `LocationProvider` seam now + land the FAB behind T-0325.** It couples a screen ticket to an
  open owner plist step for an affordance the picker doesn't need to be usable, and risks shipping a dead control if
  T-0325 hasn't landed at dispatch. **Lean DEFER** confirmed by T-0325 being `proposed`.
- **Follow-up filed:** **T-0335** (build the `LocationProvider` Core seam + the my-location FAB + auto-center; gated
  on T-0325) — `draft`, `depends_on: [T-0310, T-0325]`.

#### Scope B — Notifications-prefs: NOT buildable at parity as written; DROP from T-0310, file a separate in-app-feed spike (scope cut flagged for the PM to record)

**RULING: "Notifications prefs" is NOT buildable at parity as written. The Understand pass found NO Android prefs
surface, NO backend prefs/push-prefs API, and NO generated client for it — Android "Notifications" is an in-app push
FEED (the dashboard bell, Room-backed), NOT a Profile-tab prefs screen. DROP "Notifications prefs" from T-0310. The
in-app feed is a separate spike once a backend contract exists (T-0336). This is a scope cut flagged for the PM to
record in the ticket. No new ADR.**

- **The parity finding (confirmed).** Android partner "Notifications" is `NavRoute.Notifications`
  (`NavRoutes.kt:51-52`: *"In-app push-notifications feed — reached from the dashboard bell"*), backed by the
  partner-app-local Room DB (`core.notifications.db`, per `patterns-mobile` §"Modules": *"Has a local Room DB for
  notifications that customer-app does not"*). There is **no preferences screen** (no per-channel toggles), **no
  backend prefs/push-prefs endpoint**, and **no generated client** for one. So a "Notifications **prefs**" screen has
  **nothing to port** — it would be inventing a feature with no backend contract (an ADR-0016 standard-floor "no
  hidden/placeholder feature" risk).
- **Two distinct deferrals, kept straight.** (1) **Notifications *prefs*** → **DROPPED** (not buildable; no contract).
  (2) The **in-app notifications *feed*** (the bell → Room-backed list) was already homed to T-0310 by T-0303's §7.2
  deferral map (*"The unread-notifications DB feed + the bell badge → T-0310"*). But the feed is a **richer feature**
  (a local persistence store — the iOS Room analogue would be SwiftData/Core Data or a UserDefaults-backed cache — +
  the bell badge + a push-receipt path that depends on T-0311 APNs). Building it inside T-0310's profile scope is a
  mis-home. **Recommend a separate spike (T-0336)** to scope the in-app feed (the iOS persistence choice + the push
  contract + the bell badge) once T-0311 (APNs) lands — rather than smuggling it into the profile ticket.
- **Scope cut for the PM to record:** T-0310's title/AC should **drop "Notifications"** (both the non-existent prefs
  screen and the mis-homed feed). The Profile tab's Preferences group is **Language + Theme + Devices** (the
  `ProfileScreen.kt:183-204` parity is exactly those three rows — there is **no Notifications row** in the Android
  partner Profile hub). **Follow-up filed:** **T-0336** (spike: in-app partner notifications feed — iOS persistence
  + push contract + bell badge; after T-0311) — `draft`.

#### New / updated CRC roles (added with the T-0310 wiring)

- **`ios-profile-hub`** — the Profile tab's hub `View` + `ProfileViewModel` (in `CleansiaPartner`): *responsibility:*
  load the current employee + contract status, render the hero + section-group rows + logout, and host the Profile
  tab's `NavigationStack` over `ProfileRoute`. *Collaborators:* `PartnerProfileClient` (employee + country reads via
  the ADR-0019 spine), the section VMs (reached by route), `AppSettingsStore` (Preferences summaries),
  `SnackbarController`. *Does NOT know:* how the audience root (`PartnerRootView`) switches (it is INSIDE the
  `.dashboard` shell — it never sets the root enum); how the RegistrationLock routes (the lock owns its OWN stack);
  the device-revoke contract (SECURITY-owned); the Keychain/token.
- **`ios-profile-section`** — one per section editor (Personal/Address/Identification/Bank/Emergency/Documents): a
  section `View` + `…SectionViewModel` written ONCE, hosted by TWO stacks (the Profile tab + the lock). *responsibility:*
  load the section's fields (`UiState<T>`), validate + save (`ActionState` + a one-shot effect), and — when
  `onboarding == true` — chain forward / when `false` — pop on save. *Collaborators:* `PartnerProfileClient`, the
  `PasswordPolicy`-style field validators (localized ×5), the Address section also collaborates with the T-0306
  `MapProvider`/`GeocodingService` (the AddressPicker). *Does NOT know:* which stack hosts it (the `onboarding` flag is
  the only context it reads); the `ServiceAreaProvider` (deferred, T-0334); the audience root; whether the lock or the
  hub presented it.
- **`ios-onboarding-chain`** — the onboarding-chain VM owned by the **lock's** stack (the `OnboardingChainViewModel.kt`
  parity): *responsibility:* after a section saves in onboarding mode, re-fetch `RegistrationCompletionStatus`, decide
  the next missing section (or finish), and drive the chain header ("Step 2 of 4"). *Collaborators:* the section VMs,
  the registration-status read, the lock VM (re-resolves on pop). *Does NOT know:* the shell's Profile-tab stack (it
  drives the LOCK's stack only); how the root flips to `.dashboard` (the lock's success watermark does that).
- **`ios-app-settings-store`** (updated, sprint-12 §7.5 D1) — gains writable `language` (`setLanguage`) + a `Theme`
  enum (System/Light/Dark) + `setTheme`; still `UserDefaults`-backed, still the ONE general store. *Does NOT know:*
  (unchanged) secrets — the device id / token stay in the Keychain spine.

#### Decisions 6–8 — the Devices (Device/Mine list + revoke) security gate (SECURITY-owned; recorded 2026-06-27, security reviewer)

> **VERDICT: APPROVE-the-design with TWO BINDING implementation rules + ONE required test.** The architect's §7.7
> (D1–D5 + scope A/B) defers the device-id / revoke gate to security (note above §7.7 D1); this sub-note rules it.
> The full Gate-SEC Devices note (the S1–S10 walk + the trace evidence) lives at
> `agents/backlog/security/ios-devices.md`. The backend Device surface was traced on this Mac and is **server-scoped
> and safe** — DECISION 8 is **VERIFIED, not flagged**. The iOS work is greenfield (T-0310 not yet built — no
> `deviceMine`/`deviceRevoke` call site exists on disk yet), so these are **binding rules the developer builds to and
> the reviewer enforces**, not findings against shipped code.

**DECISION 6 — the device-id invariant (BINDING). `deviceMine(currentDeviceId:)`'s argument MUST be sourced from the
ONE `DeviceIdProvider` and nowhere else.** The `currentDeviceId:` passed to
`PartnerDeviceAPI.deviceMine(currentDeviceId:)` MUST be `DeviceIdProvider.deviceId` — the **same** instance that the
`HeaderAdapter` stamps as `X-Device-Id` (`HeaderAdapter.swift:40`) and the **same** id `Device/Register` persisted
(header-parity-contract §2). One provider per app is wired in `PartnerClients.swift:15` (`DeviceIdProvider(service:
"cz.cleansia.partner.device")`). **No second device-id source, no per-call `UUID()` mint, no `identifierForVendor`,
no re-read from a different Keychain account** (the T-0331 mint-once invariant). *Why it is load-bearing:* the server
sets `DeviceDto.isCurrent` by string-matching `currentDeviceId` against `Device.DeviceId` (`DeviceMapper.cs:14`); if
the arg drifts from the stamped `X-Device-Id`, the caller's own row shows `isCurrent == false`, the revoke trash
appears on it (DECISION 7 hides only on `isCurrent`), and the partner self-revokes a healthy session.
*Reviewer enforcement (grep):* the only expression feeding `deviceMine`'s `currentDeviceId:` is the injected
`DeviceIdProvider.deviceId` (via a `DevicesRepository.currentDeviceId` that returns it — the Android
`DevicesRepository.kt:31,34` parity); a literal, a fresh `UUID`, or any non-provider source is a FAIL.

**DECISION 7 — current-device-revoke → sign-out (BINDING; require BOTH). (a) Hide the revoke control on the current
row (mirror Android), AND (b) keep a defensive sign-out branch.** (a) The revoke trash renders **only when
`!device.isCurrent`** — the `DevicesScreen.kt:235` (`if (!device.isCurrent) { IconButton(...) }`) parity; the current
row shows a "this device" chip instead (`:221-224`). So self-revoke is **not UI-reachable**. (b) Defense in depth: if
a revoke **ever** targets the current device, the app MUST force `authClient.logout()` and return to the login/splash
root — never leave a revoked-but-logged-in session. **Detection = match the revoked row's `deviceId` against
`DeviceIdProvider.deviceId`** (the string the invariant guarantees is the current device), as the **primary** signal,
with **`isCurrent == true` as the secondary/OR signal** (the server's own flag). Match on **either** → sign out.
*Why both, and why the access token makes (b) non-optional:* the server revoke kills the refresh-token **chain** for
that `DeviceId` (`RevokeDevice.cs:44` → `RefreshTokenService.RevokeByDeviceAsync`, `:120-132`), but the in-memory
**access token keeps working until its ~15-min expiry** (`Auth.swift:292`). Without (b), a self-revoke (reachable via
a future bug, a race where `isCurrent` is stale, or a direct API call) leaves the partner in the app on a dead
session until the next 401 — exactly the "self-revokes a healthy session" footgun the header-parity contract warns
about. (a) is the parity guard; (b) is the safety net that holds even if (a) is bypassed.
*Required test (TC-IOS-DEVICES-SELF-REVOKE):* revoking a row whose `deviceId == DeviceIdProvider.deviceId` (or whose
`isCurrent == true`) drives `authClient.logout()` + a return to the login root — a unit/VM test, red-first.

**DECISION 8 — server-scoping of revoke (S2/S3 ownership) — VERIFIED on the backend (not flagged).** Traced on this
Mac: `RevokeDevice.Handler` (`RevokeDevice.cs:33-44`) derives `userId` from the JWT via `IUserSessionProvider`
(**S1** — no client-supplied id), then loads the row with `GetByIdAndUserAsync(request.DeviceRowId, userId, …)`
(`DeviceRepository.cs:21-25`, filters `d.Id == id && d.UserId == userId && d.IsActive`). A cross-user row id → `device
is null` → `DeviceNotFound` (`:38-41`) — **NotFound, not Forbidden**, so existence isn't leaked (**S3**). `GetMyDevices`
is symmetrically scoped (`GetByUserIdAsync(userId)`, `GetMyDevices.cs:22`). Every Device endpoint carries
`[Permission(Policy.Authenticated)]` + `[EnableRateLimiting("auth")]` (`DeviceController.cs:40-61`) (**S2/S5**). A
partner **cannot** revoke or even see another user's device row. **iOS adds NO client-side ownership check beyond
this** and MUST never send a `deviceRowId` it did not receive from its own `deviceMine()` response.

**Also confirmed (S4/S7/S10):** the list returns **only the caller's own** devices and the DTO leaks no foreign PII —
`DeviceDto` carries `id/platform/deviceId/lastActiveAt/isCurrent` only; **no `UserId`, no `TenantId`, no `DeviceToken`
(the push secret)** (`DTOs/DeviceDto.cs`; the iOS `DeviceDto.swift` mirror is identical). `platform`/`lastActiveAt`
are minimal, non-sensitive self-metadata. **S10/S7 (idempotent revoke):** all reads filter `&& d.IsActive`, so a
revoked (deactivated) row drops from the list; re-revoking an already-revoked row returns `DeviceNotFound` (the row is
filtered out) — the screen simply no longer shows it (the `DevicesViewModel.kt:73-75` filter-out-on-success parity).
Safe and idempotent on the already-revoked path. **`Device` implements `ITenantEntity`** (`Device.cs:6`) — the global
tenant filter applies on top of the explicit `UserId` scope (**S8**, latent multi-tenant safety; today single-tenant).

---

### 7.8 T-0307 (Phase-4 partner order work-loop) — SECURITY gate ruling (recorded 2026-06-27, security reviewer; Gate-SEC)

> **STATUS-LOG 2026-06-27 — T-0307 SECURITY GATE: order-action / ownership surface — `security_touching: YES`;
> verdict = CHANGES on ONE backend invariant + APPROVE-the-iOS-design (binding client rules). The partner-mobile
> analogue of the Devices D6-D8 ruling (§7.7). Full S1-S10 walk + binding rules in `security/ios-orders.md`.**
>
> **Greenfield, like Devices:** `phase/ios-phase4` carries **zero** committed diff (`git diff master...HEAD` = 0
> files) — no iOS order code on disk yet. So these are rules the developer builds to and the reviewer enforces,
> not findings against shipped iOS code. The **backend Order surface was traced on this Mac** (same discipline as
> Devices D8): the 10 state-changing / authorship-scoped command paths are **VERIFIED server-scoped + safe**; the
> **one gap is the `GetPaged` read** (pre-existing backend behavior T-0307 consumes — a backend fix, NOT an iOS
> regression).
>
> **D2 — action ownership scoping (S1/S2/S3) — VERIFIED safe.** Actor is JWT-derived everywhere
> (`OrderAccessService.GetCallerEmployeeIdAsync`; no command carries a client `employeeId`). Take gates on
> exists+`HasAvailableSpots`+Approved+not-already-mine (`TakeOrder.cs`); Notify/Start/Complete gate on
> `EmployeeIsAssignedToOrderAsync` + correct status (`NotifyOnTheWay.cs:73`, `StartOrder.cs:89`,
> `CompleteOrder.cs:106`). **Note/issue update·delete are AUTHOR-scoped** (`n.EmployeeId == employeeId` /
> `i.ReportedByEmployeeId == employeeId` — `UpdateOrderNote.cs:71`, `DeleteOrderNote.cs:59`, `UpdateOrderIssue.cs:68`,
> `DeleteOrderIssue.cs:59`): a second cleaner on a shared job cannot edit another's note/issue. (Action-path
> rejections surface `EmployeeNotAssignedToOrder`, not 404 — weaker than RevokeDevice's existence-hiding but
> acceptable: action denied, no customer data leaked. Logged as low-sev hardening.)
>
> **D2b — `GetPaged` read-scoping (S3/S4) — CHANGES REQUESTED (the one real gap, MEDIUM, reachable today).**
> `GetPagedOrders.Handler` builds the query purely from the **client-supplied** `Filter.EmployeeId`
> (`GetPagedOrders.cs:70` → `OrderSpecification.cs:67-70`) with **no server override pinning it to the JWT caller**
> and no "row must be mine OR available" predicate. The per-row blank (`:179-186`) hides full PII but **leaks exact
> coords / approximate address / confirmation code / the victim's pay** for non-assigned rows. **Exploit:** an
> Approved partner calls `Order/GetPaged` with `Filter.EmployeeId=<victim>` + `OrderStatuses=[Confirmed,InProgress,
> Completed]` and reads another employee's assigned-order coordinates + confirmation codes + earnings. Reachable
> because the legitimate client convention (Android `OrdersListViewModel.kt:244,249`, which iOS T-0307 mirrors) is
> "send my own id" and the backend trusts it — **the client is not authority** (Devices D8). **REQUIRED backend
> fix:** for non-admin callers force server `callerEmployeeId` on "mine" views + constrain Available to
> `HasAvailableSpots || assigned`, and blank coords/approx/confirmation-code/pay on non-assigned rows — mirror the
> existing `isAdmin ? filter.CustomerName : null` "client can't widen scope" pattern (`GetPagedOrders.cs:66-68`).
> **Backend ticket; gates the GetPaged contract for go-live. iOS UI work proceeds in parallel.**
>
> **D3 — S5/S7/S8/S10 — PASS.** All side-effecting routes carry `[EnableRateLimiting("auth")]`
> (`OrderController.cs`). Lifecycle is idempotent via status-precondition validators (stale double-action → clean
> business rejection, never crash/double-assign); Complete's money fan-out is ledger/status/idempotency-key guarded
> (Loyalty/Referral/`MessageKeys.Pay`/`MessageKeys.Receipt`). **S7a latent:** TakeOrder is a check-then-act on
> `HasAvailableSpots` with no atomic claim — TOCTOU only when shared jobs (`MaxEmployees>1`) ship; dormant today
> (single-cleaner). S8 PASS (Order tenant-filtered, envelopes carry `TenantId`). S10 PASS (`IsActive` filter).
>
> **Binding client rules (reviewer enforces):** **O1** no client employeeId on any command (orderId/noteId/issueId
> only; actor=JWT). **O2** no-client-id-echo — never send an id the client didn't receive from its own
> list/detail response (Devices D8 analogue). **O3** GetPaged "mine" is server-truth — the client may pass its own
> id as a hint but exposes no foreign-employee filter and relies on the server for isolation. **O4** on a backend
> rejection, show a clean message + refresh; re-entry-guard the action while in flight.
> **Required tests:** **TC-IOS-ORDERS-OWNERSHIP** (client VM: foreign-order action not initiated / clean reject +
> refresh; ids only from loaded models; My-tab sends only own id) + **TC-BE-ORDERS-GETPAGED-SCOPE** (backend
> integration, red-first, gates the D2b fix: caller A with `Filter.EmployeeId=B` gets zero of B's exclusive rows
> and no coords/confirmation-code/pay on non-assigned rows).
>
> **Owner follow-ups:** (1) the GetPaged read-scoping fix (REQUIRED, backend ticket); (2) TakeOrder TOCTOU atomic
> claim before shared jobs (LATENT, low pri); (3) optional NotFound-not-EmployeeNotAssigned hardening on action
> paths. The standing RefreshToken multi-tenant note (`auth-sessions.md`) is unrelated to the order surface.

---

### 7.9 T-0307 (Phase-4 partner order work-loop — OrdersList + OrderDetail + lifecycle + checklist/notes/issues/timeline) — acceptance scope + the five Understand-pass rulings (recorded 2026-06-27, architect)

> **The order-action OWNERSHIP gate is ruled by SECURITY in §7.8 (`security/ios-orders.md`, O1–O4) — this
> architect record stays OUT of it.** Decision (c) below rules only the *shape* of the presentational primary-action
> function and consumes `isMine`/`hasAfterPhotos` as inputs; how `isMine` is trusted, the no-client-id-echo rule,
> GetPaged server-truth, and the clean-reject+refresh+re-entry-guard are §7.8's O1–O4 (which this record honors).

T-0307 (`L → split`, HARD AREA #3, `phase/ios-phase4`, depends_on T-0304✓+T-0306✓) ports the partner **order
work-loop**: the 3-pane OrdersList board, the OrderDetail shell (full-bleed map + the always-present 3-snap sheet), the
**OnTheWay** lifecycle (Take → NotifyOnTheWay → Start → Complete), and checklist/notes/issues/timeline. **Slices (§6):**
A = the additive `fullBleedMap` `MapProvider` method (Core); B = OrdersList (3 panes); C = OrderDetail shell (the map +
the 3-snap sheet); D = lifecycle actions + state machine; E = checklist/notes/issues/timeline (incl. the T-0308 photo
precursor seam). The Understand pass surfaced **five** decisions. **Four APPLY accepted ADRs — no new ADR** (the
§7.2/§7.4/§7.5/§7.6/§7.7 "record, not ADR" precedent): ADR-0013 D6 + §7.6 D1 own the additive map seam (a); ADR-0013
"mirror the code" + DRY own the state machine (c); ADR-0013 parity + T-0308's home own the photo precursor (d);
ADR-0014 D2′ + §7.7 D5 + the Parity rule + the T-0310 D5 precedent own the list-state divergence (e). **ONE is a
genuine new trade-off → ADR-0021** (decision (b): the non-modal 3-snap sheet on the 16.0 floor — it could have moved the
deployment floor and it sets "the one way iOS does a non-modal map sheet," so it is an ADR, not a §7.9 line).
**Android parity source (the iOS port mirrors it):** `partner-app/.../features/orders/` —
`OrdersListScreen.kt`/`OrdersListViewModel.kt` (the 3 panes + silent-stale), `OrderDetailScreen.kt`/`OrderDetailViewModel.kt`
(the map + 3-snap sheet + the sealed `OrderDetailUiState`/`ActionState`/`OrderAction?`), `OrderPrimaryAction.kt` (the
status×ownership×photos → action), `CleaningChecklist.kt`/`…ViewModel.kt`, `StatusTimeline.kt`, `OrderStatusPill.kt`
(the `Code?.toOrderStatus()` mapping), `PhotosSection.kt` (the T-0308 precursor) + `data/orders/OrdersRepository.kt`
(the singleton per-pane/per-order staleness cache). **Gate-DP applies** (T-0307 is a screen ticket): the screens cite
their Compose counterparts; native SwiftUI; iOS-wins-on-conflict + the noted divergences (the SnapSheet-vs-`.sheet`
component swap, the SlideToCommit → native-confirm swap, the no-polygon confirmation).

**IN — T-0307 acceptance scope:**
- **Slice A:** ONE additive `MapProvider` method — `fullBleedMap(coordinate:)` (decision (a)) — implemented
  `MKMapView`-via-`UIViewRepresentable` inside `MapKitMapProvider` (ADR-0014 D6′; the only MapKit consumer).
- **Slice B:** the OrdersList — three panes (Available / MyActive / MyCompleted, the `OrdersTab` parity) over a sealed
  per-pane `UiState<[OrderListItem]>` + a 3-case refresh-phase enum, PORTING the silent-stale staleness cache
  (decision (e)); inline Active/Available row actions via an iOS-native confirm affordance (the SlideToCommit divergence).
- **Slice C:** the OrderDetail shell — the **custom non-modal 3-snap `SnapSheet`** over `fullBleedMap(coordinate:)`
  (**ADR-0021**), with the compact header always-visible drag handle + the sheet content sections.
- **Slice D:** the lifecycle — a pure `OrderPrimaryAction.action(for:isMine:hasAfterPhotos:) -> OrderPrimaryAction`
  sealed enum (decision (c)) shared by the detail footer, the list inline row, and the list panes; the OrderDetail VM is
  the sealed `OrderDetailUiState` (Loading/Error/Loaded) + `ActionState` + an `OrderAction?` in-flight discriminator
  (already canonical on Android — ported 1:1).
- **Slice E:** CleaningChecklist + NotesAndIssues + StatusTimeline, and the **T-0308 photo precursor seam**
  (decision (d)) — a DISABLED/placeholder Photos section so the Complete-blocked hint is meaningful; capture is T-0308.

**DEFERRED — explicitly out of T-0307, with the ticket each lands in:**
- **Photo CAPTURE** (camera → JSON base64 upload) → **T-0308** (decision (d): T-0307 leaves the seam — the
  `hasAfterPhotos` consumer + the reserved Photos slot — T-0308 fills capture additively, no OrderDetail re-layout).
- **The current-location FAB on any order map** → **T-0335** (gated on T-0325, the §7.6 D2 / §7.7 Scope-A line — not
  re-litigated here; the OrderDetail map is a fixed-address backdrop, no current-location needed).
- **The service-area polygon overlay** → **T-0334** (decision (a): there is **no polygon data** in the partner spec —
  add overlay support to `fullBleedMap` **additively** IF a service-area surface ever has geometry; designing it now
  repeats the speculative shape §7.6 D1 rejected).

#### Decision (a) — the additive `fullBleedMap` `MapProvider` method: `fullBleedMap(coordinate:)` — a SINGLE address pin, camera-padded for the sheet peek; NO overlay/polygon param (APPLIES ADR-0013 D6 + ADR-0014 D6′ + §7.6 D1 — no new ADR)

**RULING: the additive method is `fullBleedMap(coordinate:) -> some View` (the dev's exact Swift return type; the
contract is fixed) — it produces a full-bleed `MKMapView`-via-`UIViewRepresentable` map (ADR-0014 D6′) with ONE
annotation (the order's address pin), camera-padded at the bottom for the always-present sheet peek so the pin stays in
the visible upper sliver. NO `overlays:`/`polygon:`/`annotations:[…]` parameter. This is the §7.6 D1 "minimal-now,
additive-later" seam reaching its homed ticket (T-0306 explicitly homed the full-bleed surface to T-0307 as an additive
method) — an application of the `MapProvider` seam, no new ADR.**

- **The KEY FINDING (verified in source):** there is **NO service-area polygon data in the partner spec** — `ServiceCityDto`
  carries only `zipPrefix`, **no geometry** — and the Android `OrderDetail` renders **NO polygon**: `MapBackdrop`
  (`OrderDetailScreen.kt:256-299`) draws a **single pin** at `order.address.latitude/longitude` (a Mapbox `ViewAnnotation`,
  anchor BOTTOM, zoom 15) over the tile, nothing else. So the additive method needs **region + ONE pin**, not
  overlays/polygon.
- **The exact additive signature ruled:** `func fullBleedMap(coordinate: Coordinate) -> some View` (one address
  coordinate in; a region-bound full-bleed map view out). The **camera bottom-padding** for the sheet peek is the
  `MapBackdrop` `EdgeInsets(0,0,sheetPeekPx,0)` parity (`:273-281`) — passed from the `SnapSheet` container (ADR-0021 D1)
  so the pin sits in the unobscured upper portion. Zoom-15 default (the `:277` parity).
- **Why NO overlay/polygon now:** adding `overlays:`/`polygon:` would design `T-0334`'s (possible) service-area surface
  before any geometry exists — the **exact speculative shape §7.6 D1 rejected** for the picker. If a service-area surface
  ever has data, add overlay support **additively** then (a new method or an additive param), behind the **unchanged**
  `MapProvider` seam — one provider grows, no call site breaks.
- **The one sanctioned consumer:** the `fullBleedMap`-produced view is inside `MapKitMapProvider` (the only MapKit
  consumer alongside the §7.6 picker factory); **feature/VM code imports neither MapKit nor the view's internals**
  (reviewer #7/#12). The `SnapSheet` (ADR-0021) composes the map under the sheet; the OrderDetail feature sees only
  `fullBleedMap(coordinate:)` + `SnapSheet`, never MapKit.

#### Decision (b) — THE 3-SNAP SHEET ON THE iOS-16.0 FLOOR → a CUSTOM non-modal `SnapSheet` container in `CleansiaCore`; the floor STAYS 16.0 (the ADR-worthy one → **ADR-0021**)

**RULING: build a CUSTOM non-modal 3-snap container (`SnapSheet`) in `CleansiaCore` — a `GeometryReader` +
drag-gesture container with three snap offsets (map-focus / peek≈0.75 / expanded), layered over the always-present
full-bleed map, 16.0-safe (no `.presentationDetents`). The ADR-0014 deployment floor STAYS 16.0 (NOT bumped). Native
`.sheet`+`.presentationDetents` remain the way for MODAL sheets (the customer booking sheet). This is decision (b),
option (ii). Because it could have moved the deployment floor and it sets "the one way iOS does a non-modal map sheet,"
it is recorded as a genuine decision → ADR-0021 (extends ADR-0014 D6′ + refines ADR-0018 D3). The lower-risk fallback
(option iii — a 2-detent native modal) is recorded in ADR-0021 as the EXPLICITLY-approved-only fallback, never a silent
collapse.**

- **The trade-off (full detail + alternatives + why-not):** see **ADR-0021** — it weighs (i) bump the floor to 16.4
  (rejected: re-opens the owner's 2017-device-reach decision; and even at 16.4 a `.sheet` is **modal** — still can't be
  the always-present-over-a-live-map layer), (ii) the custom non-modal container (CHOSEN: 16.0-safe AND full layout
  parity), and (iii) collapse to a 2-detent modal (rejected as the answer — it is an ADR-0018 D1/Gate-DP **layout**
  divergence, kept only as the noted+re-approved fallback). **Why an ADR and not a §7.9 line:** unlike (a)/(c)/(d)/(e),
  this does not apply an accepted ADR — it is a new trade-off that could move the floor (ADR-0018 CH-5's bar for a
  superseding/extending ADR).
- **What ADR-0021 fixes for the dev:** the OrderDetail sheet is `SnapSheet` (the Wolt/Foodora always-present-over-map
  layer, `OrderDetailScreen.kt:172-245` parity — `skipHiddenState=true`, 0.75 peek, 3 anchors), **never** a modal
  `.sheet`; the floor stays `[.iOS(.v16)]`; the modal/non-modal discriminator (D3) keeps native `.sheet` the default
  for modal sheets. **Blocks Slice C.**

#### Decision (c) — the lifecycle STATE MACHINE: a PURE, tested `OrderPrimaryAction.action(for:isMine:hasAfterPhotos:) -> OrderPrimaryAction` sealed enum, shared by all THREE call sites (CANONICALIZES the Android inlined `when` — "the one way" — applies ADR-0013 "mirror the code" + DRY; no new ADR)

**RULING: a PURE function `OrderPrimaryAction.action(for status: OrderStatus?, isMine: Bool, hasAfterPhotos: Bool) ->
OrderPrimaryAction` returning a sealed enum, shared by the THREE call sites (the detail footer, the list Active-row
inline action, and the list panes) — NOT an inline switch per site. This canonicalizes the Android shape (which
INLINES the same `when (status)` inside the `OrderPrimaryAction` Composable, `OrderPrimaryAction.kt:54-126`, so three
iOS sites would each re-inline it) into one tested pure function — "the one way." It mirrors the Android CODE's
decision table exactly (ADR-0013 D9 "mirror the code") + DRY. Record, no new ADR. The ownership trust (§7.8 O1–O4) is
SECURITY's — this rules only the function's SHAPE.**

- **The canonical table (mirrors `OrderPrimaryAction.kt` exactly — verified; status int map in the Code→OrderStatus
  convention below):**

  | status (typed enum, via the mapping below) | isMine | → action |
  |---|---|---|
  | `_0` New / `_2` Confirmed, **not** mine | false | **`.take`** (the `!isAssignedToCurrentUser` branch, `:55-64`) |
  | `_2` Confirmed, mine | true | **`.notifyOnTheWay`** (`:65-79`) |
  | `_3` OnTheWay, mine | true | **`.start`** (`:84-98`) |
  | `_4` InProgress, mine, `hasAfterPhotos == true` | true | **`.complete`** (`:106-113`) |
  | `_4` InProgress, mine, `hasAfterPhotos == false` | true | **`.completeBlocked`** (the soft hint, `:114-122`) |
  | `_0` New, mine / `_5` Completed / `_6` Cancelled / `nil` / anything else | — | **`.none`** (no footer — `:80-83,125`) |

  The sealed enum: `enum OrderPrimaryAction { case take, notifyOnTheWay, start, complete, completeBlocked, none }`.
- **Why a shared pure function (the canonicalization, on the "earns its place" bar):** Android inlines the table in a
  Composable, so the list's inline-action path and the detail footer each re-derive "what action is valid for this row's
  status×ownership." On iOS, three call sites re-inlining the table is a drift risk (a rule change must touch three
  switches). One **pure, tested** function makes the codebase **more consistent** and the *future* lifecycle change
  (e.g. a new state) **cheaper** (one edit, one test) — it earns its place. **Rejected: an inline switch per site** —
  three copies of a lifecycle decision table.
- **`hasAfterPhotos` is the seam to decision (d):** the `.complete` vs `.completeBlocked` split consumes
  `hasAfterPhotos`, which T-0307 wires (the placeholder Photos section / the loaded order's photos report it) and T-0308
  makes real — the `canComplete` parity (`OrderPrimaryAction.kt:51,106`). The **backend validator stays the safety net**
  (the `AfterPhotosRequired` guard); `.completeBlocked` is a client-side soft hint, not the gate.
- **Scope boundary (SECURITY owns the gate, this owns the SHAPE):** whether `isMine` may be trusted, how it is derived,
  the no-client-id-echo rule, and server-side enforcement are **§7.8 (security) O1–O4** — this function consumes
  `isMine`/`hasAfterPhotos` as inputs and is purely presentational (which footer to show). The server is authoritative
  on every transition; the function never decides authority.

#### Decision (d) — the T-0308 PHOTO PRECURSOR seam: T-0307 renders a DISABLED/placeholder Photos section + the `hasAfterPhotos` consumer; T-0308 fills capture ADDITIVELY (CONFIRM the seam — applies ADR-0013 parity + T-0308's home; no new ADR)

**RULING: CONFIRMED as briefed. T-0307 leaves the photo seam so the Complete-blocked hint is meaningful, but does NOT
build capture: (1) the `OrderPrimaryAction` `.complete`/`.completeBlocked` split already consumes `hasAfterPhotos`
(decision (c)); (2) the OrderDetail sheet RESERVES the Photos slot, rendered as a DISABLED/placeholder section. T-0308
fills photo CAPTURE additively — no OrderDetail re-layout. Confirm the seam, no new ADR (Android's `PhotosSection`
sits in the OrderDetail sheet and gates Complete via `hasAfterPhotos` — the parity is the seam; T-0308 owns capture).**

- **The seam shape (verified):** Android's `PhotosSection.kt` sits in the OrderDetail sheet and the Complete action
  gates on after-photos presence (`OrderPrimaryAction.kt:106` `canComplete`). T-0307 ports the **slot + the consumer**:
  the sheet renders a Photos section in its Android position (so the layout is final — Gate-DP parity), in a
  **disabled/placeholder** state (e.g. "Photos — added in T-0308" or a non-interactive thumbnail strip), and the detail
  VM derives `hasAfterPhotos` (from the loaded order's photos, which the read DTO already carries) to feed decision (c)'s
  `.complete` vs `.completeBlocked`.
- **Why this is the right seam (not under-scoping, not over-reaching):** the Complete-blocked hint is meaningless
  without the photo concept, so T-0307 must know `hasAfterPhotos`; but **capture** (camera, JSON base64 upload,
  `SaveOrderPhotosCommand`) is a self-contained slice with its own home (T-0308) and its own size. Reserving the slot +
  wiring the consumer means T-0308 is **purely additive** — it drops the capture UI into the reserved section and the
  upload into the existing repo, with **no OrderDetail re-layout** (the §7.2/§7.3 "inert-now, additive-later" precedent).
- **The low-stakes note (recorded):** if the placeholder is interactive-looking, it risks an ADR-0016 "no dead/placeholder
  control" finding — so the placeholder must be **visibly disabled** (no tappable affordance that no-ops), the same
  inert-not-dead discipline §7.4(a)'s contact-support row took.

#### Decision (e) — the LIST STATE SHAPE: a sealed per-pane `UiState<[OrderListItem]>` + a 3-case refresh-phase enum (NOT the Android E1 flag-bag), PORTING the per-pane staleness cache; record the SlideToCommit → native-confirm divergence (applies ADR-0014 D2′ + §7.7 D5 + the Parity rule + the T-0310 D5 precedent; no new ADR)

**RULING: the iOS list is born sealed-state canonical — a sealed `UiState<[OrderListItem]>` per pane (the initial-load
lifecycle) PLUS a small explicit refresh-phase enum (`idle`/`userRefreshing`/`backgroundRefreshing`) that preserves the
silent-stale "PTR-only-on-user-pull" behavior — NOT the Android boolean flag-bag. AND: the per-pane staleness/cache
singleton (the ~30s watermarks + `invalidatePanesFor` mutation→panes mapping) is PORTED (it is load-bearing for the
no-flash resume UX) — NOT simplified to load-on-appear + `.refreshable`. The inline-action component diverges (Android
`SlideToCommit` → an iOS-native confirm affordance) — a recorded Gate-DP component swap. This applies §7.7 D5 (Android
E1 flag-bags NOT replicated on iOS) + the T-0310 D5 precedent + the Parity rule. Record, no new ADR.**

- **The Android E1 flag-bag (confirmed in source).** `OrdersListUiState` (`OrdersListViewModel.kt:89-120`) is a single
  `data class` with `isInitialLoad` + `isUserRefreshing` + `isBackgroundRefreshing` + `hasLoadedOnce` +
  `inFlightActionOrderId` + `error` + filter state — the **E1** smell (`consistency.md` E1: "never a single flag-bag
  with `isLoading`/`error` booleans"), permitting impossible states (`isInitialLoad && hasLoadedOnce`). Exactly the case
  §7.7 D5 / T-0310 D5 ruled "don't replicate; diverge correctly; file the Android fix."
- **The iOS-canonical shape (the load-bearing nuance — the silent-stale behavior must survive the refactor).** Per
  ADR-0014 D2′ + `consistency.md` E1: the pane's data-load lifecycle is a sealed `UiState<[OrderListItem]>`
  (`.loading` / `.error(canRetry:)` / `.loaded([OrderListItem])`). But the silent-stale pattern needs a SECOND axis the
  E1 enum doesn't carry — *which kind of refresh is in flight* — because the chunky pull indicator (PTR) must fire
  **only** on a user pull, never on auto/background refresh (`OrdersListViewModel.kt:73-88,222-230` — the whole point of
  splitting the flags). So the iOS shape is the sealed `UiState` **plus** a small explicit
  `enum RefreshPhase { case idle, userRefreshing, backgroundRefreshing }`: PTR binds to `== .userRefreshing` (the
  `isUserRefreshing` parity), `.backgroundRefreshing` is invisible (no chunky indicator — the silent-stale resume), and
  `.loaded` content stays mounted through a background refresh (no spinner flash). **Two orthogonal sealed states**,
  not a flag-bag — preserving the behavior while removing the impossible states. (Per-pane: each of the three panes
  carries its own `UiState` + `RefreshPhase`, the independent-pane parity.)
- **The per-pane staleness cache is PORTED (the parity rule — a behavior divergence must be explicitly approved, and
  this one is NOT approved to drop).** `OrdersRepository` is a `@Singleton` with **three independent per-pane `Staleness`
  watermarks** (default 30s) + a per-order watermark + `invalidatePanesFor(mutation)` mapping each mutation to the panes
  it changes (Take/NotifyOnTheWay → Available+Active; Start → Active; Complete → Active+History; the third pane stays
  warm — `OrdersRepository.kt:159-192`). This is **load-bearing for the no-flash resume UX**: returning from OrderDetail
  after a take/start/complete refreshes silently (warm cache = no-op; stale = background fetch, no chunky indicator) and
  a mutation invalidates exactly the affected panes. **It is PORTED to iOS** — an actor/class with the same per-pane/
  per-order watermarks + the same mutation→panes map, registered in the `SessionScopedCacheRegistry` (cleared on
  sign-out). **Simplifying to load-on-appear + `.refreshable` for v1 is a behavior divergence (flash-on-resume, lost
  cross-pane invalidation) — REJECTED, not silently dropped** (the Parity rule: a behavior difference must be
  ticket-called-for and approved; this one is explicitly NOT approved). The §7.2/§7.4 dashboard-cache deferral does
  **not** apply here — that was the proving vertical with no resume loop; the order work-loop's resume-after-mutation
  **is** the cache's reason to exist.
- **The recorded Gate-DP divergence (architect sign-off) — SlideToCommit → native confirm affordance.** Android's
  inline + footer commit gesture is a custom `SlideToCommit` slider (`SlideToCommit.kt`). iOS uses an **iOS-native
  confirm affordance** for the same commit (a native button + `.confirmationDialog`/haptic; the list inline =
  `swipeActions` row action) — the ADR-0018 D3 component swap (custom gesture → native control), keeping the same action
  set, the same in-flight spinner semantics (`inFlightActionOrderId` parity), the same one-action-at-a-time guard
  (`OrdersListViewModel.kt:329`, the §7.8 O4 re-entry guard). **Noted divergence:** *"Android SlideToCommit → iOS-native
  confirm affordance; same commit actions/in-flight/guard; the divergence touches the component, not
  layout/flow/branding"* — passes Gate-DP #3.
- **Android E1 fix filed:** the `OrdersListUiState` flag-bag fix folds into the existing partner state-cleanup
  follow-up **T-0337** (the §7.7 D5 home for Android partner E1 flag-bags) — one android cleanup ticket, independent of
  the iOS wave.

#### The Code→OrderStatus mapping convention (the read-path DTO envelope — a small canonical rule across the orders surface)

**RULING (recorded as a convention, harvested to `patterns-mobile`):** the generated read-path DTOs (`OrderItem.orderStatus`,
`OrderListItem.orderStatus`) carry a **`Code` envelope** (`{type, name, value: Int?}`), NOT the typed `OrderStatus`
enum (the generated action responses — `StartOrderResponse.newStatus`, `CompleteOrderResponse.newStatus`,
`NotifyOnTheWayResponse.newStatus` — DO carry the typed `OrderStatus`). Map the envelope to the typed enum in
**exactly one** place — a Core (or orders-feature) extension:

```swift
// The Code envelope → typed OrderStatus, the ONE mapping. OrderStatus is `: Int` with rawValues 0...6
// matching the backend ints (OrderStatus.swift). The Android `Code?.toOrderStatus()` parity (OrderStatusPill.kt:40-42).
extension Code {
    func toOrderStatus() -> OrderStatus? { value.flatMap(OrderStatus.init(rawValue:)) }
}
// Backend int map (OrderStatusPill.kt:36-37, the UI single source of truth):
// 0 New · 1 Pending · 2 Confirmed · 3 OnTheWay · 4 InProgress · 5 Completed · 6 Cancelled
```

- **Why one place:** every status read (the pill, the timeline, decision (c)'s action table, the pane filters) needs the
  typed enum; re-deriving `Code.value` → enum at each site is drift (and a nil-`value` foot-gun). One extension is "the
  one way," tested once. **Deviation a reviewer rejects:** a call site reading `orderStatus.value` and comparing the raw
  `Int` (e.g. `== 4`) instead of mapping to `OrderStatus._4`, or a second `Code→OrderStatus` mapper.
- **The pane filters use the typed enum for the query** (the `OrdersListViewModel.kt:235-251` parity: Available =
  `[_0,_2]` + `isUnassigned`; MyActive = `[_2,_3,_4]` + employeeId; MyCompleted = `[_5]` + employeeId) — sent as the
  `filterOrderStatuses` ints, mirroring Android (the employeeId is a hint; the server is truth — §7.8 O3).

#### The recorded Gate-DP divergences (T-0307 — all component-only, none touch layout/flow/branding)

1. **The OrderDetail sheet: Compose `BottomSheetScaffold` (non-modal 3-snap) → the custom `SnapSheet` Core container**
   (NOT a modal `.sheet`) — the iOS-16.0-floor decision, **ADR-0021** (the detent decision). Same
   always-present-over-map layout, 3 anchors, 0.75 peek.
2. **Commit gesture: `SlideToCommit` slider → iOS-native confirm affordance** (button + confirmation/haptic; list inline
   = `swipeActions`) — decision (e). Same actions, in-flight, guard.
3. **No service-area polygon** (and no polygon param on `fullBleedMap`) — decision (a): there is no polygon data in the
   partner spec and Android renders none; overlay support is additive IF T-0334 ever has geometry. (Recorded so a
   reviewer does not flag the absence — Android has none either.)

#### Reviewer-check additions (T-0307+)

- **#29 (ADR-0021) — the OrderDetail sheet is the custom non-modal `SnapSheet`, not a modal `.sheet`.** Layered over the
  always-present full-bleed map, 3 snap offsets (map-focus/peek≈0.75/expanded) via drag, no `.sheet`/dismiss state
  (`skipHiddenState=true` parity), map camera bottom-padded by the sheet offset. **Findings:** a modal `.sheet` for
  OrderDetail (an ADR-0018 D1/Gate-DP layout divergence); a 2-anchor collapse without the noted+re-approved fallback; a
  `SnapSheet` used for a genuinely modal sheet (D3 boundary); any `@available(iOS 16.4)` that lifts the floor (the floor
  stays 16.0). **Test: TC-IOS-SNAP** (the pure snap-offset resolver — see ADR-0021).
- **#30 (§7.9 (a)/(e)) — the order map + list state are the canonical shapes.** (a) `fullBleedMap(coordinate:)` is one
  additive `MapProvider` method (single pin, no polygon param) inside `MapKitMapProvider`; feature/VM import no MapKit
  (composes with #7/#12). (e) the list is a sealed per-pane `UiState<[OrderListItem]>` + a `RefreshPhase` enum (NOT a
  flag-bag struct), the per-pane/per-order staleness cache is PORTED (not load-on-appear), and the commit affordance is
  iOS-native (the SlideToCommit divergence, noted). **Findings:** a ported `OrdersListUiState` flag-bag; PTR firing on
  background refresh; dropping the staleness cache without explicit approval; a `fullBleedMap` overlay/polygon param
  with no data; a feature `import MapKit`.
- **#31 (§7.9 (c)/Code-convention) — the lifecycle action + status mapping are the one way.** (c) the primary action is
  the pure shared `OrderPrimaryAction.action(for:isMine:hasAfterPhotos:)` (not three inline switches); the function is
  presentational and consumes `isMine`/`hasAfterPhotos` (ownership trust = §7.8 O1–O4). Code→OrderStatus is mapped in
  exactly one `Code.toOrderStatus()` extension (no raw-`Int` `.value` comparisons; no second mapper). **Findings:** an
  inline per-site action switch; a raw `orderStatus.value == N` comparison; a second status mapper. **Test:
  TC-IOS-ORDER-ACTION** (the pure action table — every row of the decision (c) table, incl. `nil`/Completed/Cancelled →
  `.none` and `_4`+mine+!photos → `.completeBlocked`).

#### New / updated CRC roles (added with the T-0307 wiring)

- **`ios-snap-sheet`** (new, `CleansiaCore/Components`) — the `SnapSheet` non-modal 3-snap container (ADR-0021):
  *responsibility:* render content over a full-bleed backdrop parked at one of three drag-driven snap offsets
  (map-focus/peek/expanded) and report its current offset for backdrop camera-padding. *Collaborators:* the host view
  (which supplies the backdrop + content), `GeometryReader`/`DragGesture`. *Does NOT know:* what's in the content or the
  backdrop (order data, MapKit), whether it's an order or a future map screen, or any feature/VM logic.
- **`ios-order-primary-action`** (new, partner orders feature) — the pure `OrderPrimaryAction.action(for:isMine:hasAfterPhotos:)`
  table (decision (c)): *responsibility:* map status×ownership×after-photos → the one valid primary action (a sealed
  enum), mirroring `OrderPrimaryAction.kt`. *Collaborators:* none (pure function over value inputs). *Does NOT know:*
  how `isMine`/`hasAfterPhotos` are trusted (SECURITY §7.8 owns the ownership gate; the server is authoritative), how
  the action is rendered (footer vs swipe), or how it is dispatched.
- **`ios-orders-cache`** (new, the `OrdersRepository` staleness parity) — *responsibility:* hold the per-pane (~30s) +
  per-order freshness watermarks, answer `isPaneStale`/`isOrderStale`, and `invalidatePanesFor(mutation)` mapping each
  mutation to the panes it changes. *Collaborators:* the orders generated client (via the ADR-0019 spine), the
  `SessionScopedCacheRegistry` (cleared on sign-out). *Does NOT know:* the UI refresh-phase (that's the VM's
  `RefreshPhase`), which pane is on screen, or the token/device id (the Keychain spine).
- **`ios-order-detail-vm`** (new) — the sealed `OrderDetailUiState` (Loading/Error/Loaded) + `ActionState` +
  `OrderAction?` in-flight (the `OrderDetailViewModel.kt` parity — already canonical on Android, ported 1:1):
  *responsibility:* load/refresh the order (silent-stale via `ios-orders-cache`), run lifecycle actions, derive
  `hasAfterPhotos`, surface success/error via the `SnackbarController`. *Collaborators:* `ios-orders-cache`,
  `ios-order-primary-action` (for the footer's action), `SnackbarController`, the orders client. *Does NOT know:* the
  sheet's snap mechanics (`ios-snap-sheet`), MapKit (`fullBleedMap`), or photo capture (T-0308).

---

## 8. Gates & verification (per `agents/process/quality-gates.md`)

- **Reviewer-per-developer** on every ticket (concurrent).
- **Security gate mandatory on T-0300** (the auth/session/header spine — a wrong anon allow-list leaks a
  Bearer to anon endpoints; a second device-id source breaks remote-revoke; a reused refresh token
  self-revokes), **T-0313** (card/payment flow), **T-0314** (GDPR delete/export + dispute evidence).
- **TDD on the auth spine:** TC-IOS-AUTH-401 (single-flight), TC-IOS-ANON (no-Bearer-on-anon),
  TC-IOS-DEVICEID (header==Device/Register id), TC-IOS-EMPTYTOKEN (200+empty→confirm gate), TC-IOS-STATE
  (the three UiState cases) — **red-first**.
- **TDD on the generated-client auth bridge (ADR-0019, T-0303 + every authed wave):** TC-IOS-GEN-AUTH (a
  generated `dashboardGetStats` carries the Bearer + device/time-zone headers **despite** the generated
  `requiresAuthentication: false` — the factory, not the flag, governs), TC-IOS-GEN-401 (N concurrent generated
  401s → exactly one refresh via the same `SessionRefresher`; queued callers retry with the rotated token),
  TC-IOS-GEN-DEVICEID (the generated call's `X-Device-Id` == the `Device/Register` deviceId — one source) —
  **red-first**. **✅ GREEN in T-0303 (`2a57f70`):** the **TC-IOS-GEN** bridge suite passes — a generated call
  carries Bearer + `X-Device-Id`/`X-Device-Label`/`X-Time-Zone` despite `requiresAuthentication:false`, and a
  401 drives a single-flight refresh + **exactly one** retry with the rotated token (§7.3). Required §7.2
  router-gate test (`requiresEmailConfirmation==true` → `verifyEmail`) also present + green.
- **TDD on the partner shell + gate (T-0304, §7.4 + ADR-0020):** **TC-IOS-REGLOCK** (empty/nil status →
  LOCKED; each single-wrong field → LOCKED — profile/docs false, contract Pending(1)/Terminated(3)/
  Rejected(5)/nil; `.failure` → LOCKED on **both** the SplashGate and the lock-VM paths; only
  profile+docs+Approved(4)|Active(2) → UNLOCKED), **TC-IOS-ROUTER-SEED** (`.splash` when `hasValidSession`
  else `.login` — never `.dashboard`), **TC-IOS-ROUTER-BOUNCE** (verified login → `.splash`; unverified →
  `.verifyEmail` — the extended §7.2 gate), **TC-IOS-SPLASH-RESOLVE** (complete → `.dashboard`, incomplete/
  `.failure` → `.registrationLock`, no-session → `.login`) — **red-first**. **✅ GREEN in T-0304 (`c269360`):**
  **TC-IOS-REGLOCK** passes — the AND predicate with any-nil→LOCKED (availability not a clause) and BOTH
  fail-closed error paths (SplashGate `.failure`→lock; lock-VM `.failure` preserves last-known and never
  unlocks). `swiftformat --lint` + `swiftlint --strict` clean; **CleansiaCore 93 + CleansiaPartner 61** tests
  pass on the iPhone 17 simulator.
- **TDD on the partner auth completeness (T-0305, §7.5):** **TC-IOS-CONFIRM-PUT** (the spine sends
  `ConfirmUserEmail` as **PUT** — a method param defaulting `.post`; a request asserting the verb is PUT, and
  that no other auth path's verb changed) ; **TC-IOS-ANON** extended to the **confirm double-skip** (a stored
  Bearer is NOT attached on `ConfirmUserEmail`/`Register`/`ResendConfirmationEmail`/`ForgotPassword`; the
  device/tz headers ARE) ; **TC-IOS-EMPTYTOKEN** extended to the **confirm path** (confirm `200`+empty Token →
  `unverifiedEmail(hasToken:false)` → no session / `error_generic`; `200`+token → authenticated → bounce
  through `.splash`) ; **TC-IOS-SETTINGS** (`hasSeenOnboarding` get/`markSeen` round-trips via UserDefaults and
  resets on a fresh store; the language resolver returns the persisted in-set tag, else the `Locale.current`
  seed if in-set, else `"en"`) ; **TC-IOS-PASSWORD-POLICY** (`PasswordPolicy` is exactly ≥8 && ≥1 letter && ≥1
  digit — the `RegisterViewModel.kt:37-39` parity: rejects `"short1"`, `"12345678"` (no letter),
  `"abcdefgh"` (no digit); accepts `"abcdefg1"`) ; **TC-IOS-VERIFY-EMAIL-ARG** (`Route.afterLogin` seeds
  `.verifyEmail(email:)` from the unverified-login email; a cold-start `.verifyEmail(nil)` disables resend +
  shows `error_generic`, and the `requiresEmailConfirmation==true → .verifyEmail` gate is preserved) —
  **red-first**. **✅ GREEN in T-0305 (`ccd25cd`+`e232147`+`3e70cdb`+`84d38bc`):** **TC-IOS-CONFIRM-PUT**
  (ConfirmUserEmail sent **PUT** via the new `send()` `httpMethod:` param defaulting `.post`; no other auth
  verb changed), **TC-IOS-SETTINGS** (`AppSettingsStore` `hasSeenOnboarding` get/`markSeen` round-trips via
  UserDefaults + resets on a fresh store; the language resolver returns the persisted in-set tag, else the
  `Locale.current` seed if in-set, else `"en"`), **TC-IOS-PASSWORD-POLICY** (the Core `PasswordPolicy` is
  exactly ≥8 && ≥1 letter && ≥1 digit — the `RegisterViewModel.kt:37-39` parity), and the **extended
  TC-IOS-ANON** (the **double-skip** — a stored Bearer is NOT attached on `ConfirmUserEmail`/`Register`/
  `ResendConfirmationEmail`/`ForgotPassword`, device/tz headers ARE; a **positive-control** asserts the test
  is non-tautological) **/ TC-IOS-EMPTYTOKEN** (confirm `200`+empty → `unverifiedEmail(hasToken:false)`, no
  session; `200`+token → authenticated → bounce through `.splash`) **/ TC-IOS-VERIFY-EMAIL-ARG** all pass on
  the iPhone 17 simulator (**CleansiaCore 114 + CleansiaPartner 96**).
- **Reviewer compliance checks (ADR-0013 + ADR-0014 §"How a reviewer verifies"):** #1 no hand-edited
  generated client · #2 auth NOT generated · #3 X-Device-Id single source · #4 anon allow-list complete
  (incl. customer host) · #5 refresh token replaced every refresh · #6 single no-auth session +
  single-flight · #7 maps behind `MapProvider` (no direct MapKit/Mapbox import in features) · #8 one
  `ApiResult`/`ApiError` in `CleansiaCore` (ADR-0011 D4) · #9 OnTheWay lifecycle parity (mirror the code) ·
  #10 i18n 5-locale completeness, no hardcoded strings · **#11 (ADR-0014) deployment target = iOS 16; no
  `import Observation`/`@Observable`/`@available(iOS 17)` always-on path — VMs conform to `ObservableObject`
  with `@Published` state, `@StateObject` for owned VMs vs `@ObservedObject` for injected (the foot-gun)** ·
  **#12 (ADR-0014) no iOS-17-only SwiftUI MapKit API (`Map {...}` content builder / `Marker`/`MapPolygon`/
  `MapCameraPosition`) — rich-map surfaces via `MKMapView` inside `MapKitMapProvider`** ·
  **#13-gen (ADR-0019) the generated business client authenticates ONLY via the Core-spine-backed
  `RequestBuilderFactory` installed into the generated config — NO second token source (no wrapper/call-site
  reading `TokenStore`, setting `Authorization`/`Bearer`, or writing a Bearer into `customHeaders`), NO per-call
  header duplication, NO per-call 401 handling; the injected `AnonymousAllowList` (not the generated
  `requiresAuthentication` flag) governs**. **✅ #13-gen PASS in T-0303 (reviewer, both slices)** — single
  token source; no per-call header/token code outside `HeaderAdapter`; no hand-edited generated client. ·
  **#23 (ADR-0020, T-0304+) partner top-level audience routing is the flat-enum `PartnerRootView`
  root-switch gated by `.splash`** — the audience is a closed `enum` switch (not a pushed
  `NavigationPath`); seeded `hasValidSession ? .splash : .login` (NOT `.dashboard`); a verified login bounces
  through `.splash` (NOT straight to `.dashboard`); there is **no** login→shell path bypassing `.splash`;
  `NavigationStack` is the **intra-audience** push container only. **✅ #23 PASS in T-0304 (reviewer,
  `c269360`)** — the flat-enum `PartnerRootView` root-switch reseeded `.dashboard`→`.splash`, closing a
  latent T-0303 fail-OPEN (authed-but-incomplete partner no longer lands on the authed area); ADR-0020
  canonicalized in `55b39aa`. · **#24 (§7.4 Decision 1, T-0304+ —
  SECURITY) the partner registration gate is fail-closed end-to-end** — the predicate is the **AND** of
  profile + documents + contract∈{Approved(4),Active(2)} with **every** nil/unknown/other → LOCKED (no
  permissive optional default; availability is NOT a clause); the SplashGate routes a status-API `.failure`
  to the lock **never** the shell; the lock VM's `.failure` **preserves** the cached status and **never**
  unlocks (only the success "complete" watermark unlocks). A permissive nil default, a `.failure` reaching
  the shell, or a `.failure` clearing/unlocking is a **blocking** finding. **✅ #24 PASS in T-0304 (reviewer
  + security APPROVE, `c269360`)** — the AND predicate with every nil/unknown→LOCKED (availability not read);
  SplashGate `.failure`→lock; lock-VM `.failure` preserves the cached status and never unlocks; **TC-IOS-REGLOCK
  green**. Security traced the backend: `CheckCurrentEmployee` is **token-scoped + `[Permission]`-guarded, no
  client-supplied id** (§7.3 fwd-note #2 holds — any server-derived id round-trip is safe only by the
  server-side override).
  · **#25 (§7.5 Decision 3, T-0305+ — SECURITY/plumbing) the auth-completeness slice preserves the spine's
  anon/PUT/empty-token contract:** (a) **no new partner anon allow-list entry** — the four T-0305 paths are
  already in `AnonymousAllowList.sharedAuth`; the partner host stays auth-only; `Logout` stays AUTHED (not on
  the list); (b) **`ConfirmUserEmail` is sent as PUT** via a `send()` HTTP-method param defaulting `.post`
  (no silent 405; no other path's verb changed); (c) the **double-skip** holds — a stored Bearer is NOT
  attached on any of the four anon paths (confirm is the sharpest case: a token IS present post-login), while
  `X-Device-Id`/`X-Device-Label`/`X-Time-Zone` ARE sent; (d) **confirm reuses the empty-token gate**
  (200+empty → `unverifiedEmail(hasToken:false)`, no session; 200+token → authenticated). A Bearer leaking
  onto an anon path, a hardcoded-POST 405 on confirm, a parallel confirm-specific gate, or a 200+empty-token
  confirm entering the app is a **blocking** finding. **✅ #25 PASS in T-0305 (reviewer + security APPROVE,
  Slice A `e232147`)** — no new anon entry (the four paths already in `AnonymousAllowList.sharedAuth`), Logout
  stays authed; ConfirmUserEmail sent **PUT** via the new `send()` `httpMethod:` param (no silent 405, no
  other verb changed); the **double-skip** holds (stored Bearer NOT attached on the four anon paths, device/tz
  headers ARE — a **positive-control** proves it non-tautological); confirm reuses the empty-token gate. **Security
  traced the backend `ConfirmUserEmail` handler** and confirmed it resolves the user from the confirmation
  **CODE alone** (no session identity needed → the anon double-skip is SAFE). · **#26 (§7.5 Decisions 1/2/4, T-0305+) the
  auth-completeness seams are the canonical ones:** (a) device-local prefs (onboarding-seen, language tag)
  read/write the single **`AppSettingsStore`** in `CleansiaCore` (UserDefaults-backed, NOT Keychain) — a
  second settings store, or onboarding-seen/language in the Keychain, is a finding; (b) the ConfirmEmail
  resend email rides the **`.verifyEmail(email:)`** Route associated value — a NEW `UserProfileStore` built
  to carry it is a finding (the email is a nav input, not a profile store); the cold-start `.verifyEmail(nil)`
  path disables resend + shows `error_generic`; (c) the password rule is the Core **`PasswordPolicy`**
  (≥8 && ≥1 letter && ≥1 digit) feeding the Core **`PasswordRuleList`** — a VM-local copy of the predicate
  (the Android `RegisterUiState` smell) or a per-app password widget is a finding; (d) every validation
  message is an `.xcstrings` key ×5 (the F1 fix — NO hardcoded validation strings; reviewer #10 i18n). **✅ #26
  PASS in T-0305 (reviewer, all slices)** — (a) onboarding-seen + the language tag read/write the single Core
  `AppSettingsStore` (UserDefaults-backed, not Keychain); (b) the resend email rides `.verifyEmail(email:)` —
  **no `UserProfileStore` introduced**; the cold-start `.verifyEmail(nil)` path disables resend + shows
  `error_generic`; (c) the Core `PasswordPolicy` (≥8 && letter && digit) feeds the Core `PasswordRuleList`,
  shared partner+customer, no VM-local copy; (d) every validation message is an `.xcstrings` key ×5 (the F1
  divergence — Android's hardcoded literals NOT replicated; android fix filed as **T-0333**).
  · **#27 (§7.6, T-0306+ — maps) the map seam is the canonical one** (composes with #7/#12): (a) all map +
  geocode use goes through the Core **`MapProvider`/`GeocodingService`** protocols — **no feature/VM
  `import MapKit` or `import CoreLocation`**; the `MapKitMapProvider`-produced view + `CLGeocoderGeocoding
  Service` are the **only** MapKit/CoreLocation consumers (a second consumer is a finding); (b) the seam ships
  **minimal** — the picker factory only; T-0307's full-bleed/overlay surface is an **additive** method (a
  designed-ahead richer `MapProvider` is the rejected shape); (c) **NO iOS-17-only `Map{Marker}`/`MapPolygon`/
  `onMapCameraChange`** (#12) — the picker uses `Map(coordinateRegion:annotationItems:[])` + a SwiftUI overlay
  pin; (d) geocoding is **best-effort** (nil/`[]` on error, **cancel-before-refire** for
  `kCLErrorGeocodeCanceled`, never blocks the confirm/crashes), debounce **300ms forward / 500ms reverse**
  ported VERBATIM (reverse-on-idle is a VM Combine/`Task` debounce, not a map callback); (e) the AddressPicker
  has **NO `UiState`/`ActionState`** — plain `@Published` + a one-shot `onConfirmed` callback; **the
  sealed-state absence is correct — do NOT flag it**; (f) **current-location/the my-location FAB are NOT in
  T-0306** (deferred to T-0310 + T-0325's plist key) — building the FAB now (a dead control without the plist
  key) is a finding; the recorded **Gate-DP divergence** (iOS omits current-location pending T-0325; pan/search
  parity full; touches the affordance, not layout/flow/branding) is noted in-ticket; (g) **NO Mapbox token /
  map SDK / `Package.swift` map entry** — a stray token or `MapStyles.kt` port (Q-IOS-02 is "No") is a finding.
  · **#28 (§7.7, T-0310+ — partner Profile tab) the Profile tab + lock-routing + settings + state seams are the
  canonical ones:** **(a) nav structure (D1)** — the Profile tab is an **in-tab `NavigationStack` over a typed
  `ProfileRoute` enum** INSIDE the `.dashboard` shell (the ADR-0020 #23 intra-audience push); audience states
  (`Splash/Login/Lock/Main`) are **never** `ProfileRoute` cases, and a section push modeled as an audience hop (or the
  audience as a `ProfileRoute`) is a finding; the `onboarding: Bool` payload is on the gate-section cases only
  (Personal/Address/Identification/Bank), Emergency/Documents carry none; the AddressPicker is a `.sheet`/`.fullScreenCover`
  return-value flow (`onConfirmed`), **not** a back-stack route. **(b) lock routing (D2 — the load-bearing call)** — the
  `RegistrationLock` (root audience state, NOT in the shell) owns its **OWN** local `NavigationStack` + onboarding-chain
  VM and pushes the **SHARED** section set over **itself** with `onboarding == true`; on pop it re-resolves and **only**
  the success watermark flips the root to `.dashboard`; **no Fix CTA renders or routes into the shell's Profile-tab
  stack** (a shell reach before complete is a fail-OPEN finding — composes with **#24**); a second forked copy of any
  section View/VM for the lock is a finding (one set, two hosts, the `onboarding` flag is the switch). **(c) settings
  (D4)** — language + theme read/write the **one** `AppSettingsStore` (UserDefaults, NOT Keychain — #26a sibling); a
  second settings store or either pref in the Keychain is a finding; the theme is honored via `.preferredColorScheme`
  on the root (no dead Preferences row). **(d) state (D5)** — the Profile hub + each section's load are sealed
  `UiState<T>`, the save is `ActionState` + a one-shot effect; a **ported flag-bag `…UiState` struct** (the Android E1
  shape) or a **hardcoded validation/error string** (every message an `.xcstrings` key ×5, #10) is a finding. **(e)
  scope (A/B)** — the my-location FAB is **NOT** in T-0310 (deferred → T-0335 gated on T-0325's plist key; building it
  now is a dead-control finding), and there is **no "Notifications" row/screen** in the partner Profile hub (the
  Preferences group is Language + Theme + Devices — the `ProfileScreen.kt:183-204` parity); a Notifications-prefs screen
  with no backend contract is a hidden/placeholder finding (ADR-0016 floor). The **`ServiceAreaRow`** is deferred
  (T-0334) — its absence is the recorded **Gate-DP divergence** (advisory affordance, not a save gate), **not** a
  finding.
- **TDD on the partner Profile tab + lock routing (T-0310, §7.7):** **TC-IOS-PROFILE-ROUTE** (the Profile tab's
  `NavigationStack` pushes/pops `ProfileRoute` cases; an audience state is never a `ProfileRoute`); **TC-IOS-LOCK-CHAIN**
  (the lock pushes a section with `onboarding == true` → save chains to the next missing section; chain-finish pops back
  to the lock; the lock re-resolves and only `isRegistrationComplete()` flips the root to `.dashboard`; **no path reaches
  the shell's Profile-tab stack from the lock** — the fail-closed assertion, composes with TC-IOS-REGLOCK); **TC-IOS-SECTION-SHARED**
  (the SAME section View/VM, hosted from the Profile-tab stack with `onboarding == false`, pops on save — proving one set,
  two hosts); **TC-IOS-SETTINGS-THEME** (`setTheme`/`setLanguage` round-trip the one `AppSettingsStore` via UserDefaults;
  `.system`→`nil` colorScheme, `.light`/`.dark` map through; resets on a fresh store); **TC-IOS-PROFILE-STATE** (the hub +
  a section load render sealed `UiState<T>` `.loading`/`.error`/`.loaded`; a save renders `ActionState` + a one-shot
  effect — no flag-bag) — **red-first**.
- **Mechanical:** the Xcode workspace builds; `CleansiaCore` + both app targets compile; the codegen step
  produces the client from the on-disk spec (no hand-edit); the Swift test suites run. **✅ T-0303 evidence:**
  `swiftformat --lint` + `swiftlint --strict` clean; **CleansiaCore 93 + CleansiaPartner 17** tests pass on
  the iPhone 17 simulator; the T-0302 first real generation (`8d4cfe3`) produced the client from the
  regenerated on-disk spec (`9232335`), no hand-edit. **✅ T-0304 evidence (`55b39aa`+`c269360`+`df71181`):**
  `swiftformat --lint` + `swiftlint --strict` clean; **CleansiaCore 93 + CleansiaPartner 61** tests pass on
  the iPhone 17 simulator (the partner suite grew 17→61 with the gate/router/shell tests incl. TC-IOS-REGLOCK).
  **✅ T-0305 evidence (`ccd25cd`+`e232147`+`3e70cdb`+`84d38bc`):** `swiftformat --lint` + `swiftlint --strict`
  clean; **CleansiaCore 114 + CleansiaPartner 96** tests pass on the iPhone 17 simulator (CleansiaCore grew
  93→114 with `AppSettingsStore`/`PasswordPolicy`; CleansiaPartner grew 61→96 with the
  Register/Forgot/ConfirmEmail/Onboarding + the confirm-PUT/anon-double-skip/empty-token tests); the auth paths
  stay hand-written + anon, the spine `send()` gained only the `httpMethod:` param — no codegen / no
  hand-edit.

---

## 9. Definition of wave-done (rolling — this is a multi-phase port, not a single sprint)

Phase 0 done = the workspace + `CleansiaCore` + the auth/session/header spine + DI + snackbar/error +
the codegen toolchain all build, with the auth contract tests green. Phase 1 done = partner login →
read-only Dashboard works end-to-end against the **regenerated** client, proving the architecture —
**✅ SATISFIED 2026-06-26: T-0303 `done` (`8996df9`+`2a57f70` on `phase/ios-phase1`); the spec was
regenerated (`9232335`) and the dev mobile API is live (the two §7.1 blockers cleared).** Each
Phase-2+ feature ticket has an owner, a current state, satisfied-or-blocked deps, AC↔evidence, the
ADR-0013 reviewer checks green, and a status-log line per transition. INDEX.md + this doc match reality.
The three non-blocking owner questions (Q-IOS-01/02/03) are tracked with their defaults; the
mobile-spec-regen is confirmed before any generated-client ticket advances to `done`.

---

## 10. APPLE APP REVIEW COMPLIANCE + the iOS quality bar (ADR-0016, added 2026-06-23)

**Source:** **ADR-0016** (`adr/0016-apple-app-review-compliance-and-ios-quality-bar.md`, **accepted**
2026-06-23). The owner wants the iOS apps held to a **submission-passing** bar (much higher than the rest of the
platform). **Framing (stated so the myth dies): there is NO "AI-written-code detector" and App Review cannot
brick hardware — both FALSE.** The real risk is **rejection vs the published App Store Review Guidelines** +
account-level consequences for concealment/abuse. This section engineers for the **knowable checklist**.

**Ticket ids T-0323…T-0329** (continuing after the iOS port tickets; the Azure wave, sprint-13, takes
T-0315…T-0322 — see the numbering note in sprint-13 §intro).

### 10.1 The real obligations (traced — ADR-0016 D2)

- **Sign in with Apple is REQUIRED on the customer app (Guideline 4.8)** because the customer app offers
  **Google Sign-In** (`customer-app/.../auth/AuthModule.kt`, `SignUpScreen.kt`). The **partner** app has no
  social login → **no SIWA obligation.** The *integration mechanism* (a backend `appleauth` endpoint?) is
  **Q-IOS-04** (owner), which gates only the SIWA ticket.
- **In-app account deletion is REQUIRED on the customer app (Guideline 5.1.1(v))** — the existing GDPR/
  `GdprDeletionService` delete flow must be reachable **in-app** from Settings and actually delete account+data.
- **External Stripe payment is ALLOWED and IAP must NOT be used (3.1.3/3.1.5)** — cleaning is a **real-world
  service**; documented so a reviewer back-and-forth does not wrongly demand IAP. (`SubscribePlus` watch-item.)
- **Privacy cluster:** `PrivacyInfo.xcprivacy` manifest per target (required-reason APIs, data types,
  **tracking=false**); App Privacy nutrition label matching it; **no ATT prompt** (the apps don't track for
  ads); Info.plist **purpose strings** (location for the MapKit pickers; camera + photo library for partner
  photos T-0308 + customer dispute evidence T-0314) localized ×5. **Push uses the `aps-environment` entitlement
  + the runtime `UNUserNotificationCenter` request — NOT an Info.plist key** (no `NSUserNotificationsUsageDescription`).
- **Standard floor + quality:** no private APIs, no hidden/disabled features, complete metadata + a demo
  account, functional + crash-free, no placeholder content; HIG/accessibility (VoiceOver, Dynamic Type,
  contrast).

### 10.2 Compliance ticket table (ADR-0016 D3)

| ID | Title | Size | Status | Layers | depends_on | manual_step | When (rel. to the iOS phases) |
|----|-------|------|--------|--------|-----------|-------------|-------------------------------|
| **T-0323** | **SwiftLint + SwiftFormat BLOCKING iOS CI gate** — `src/cleansia_ios/.swiftlint.yml` + `.swiftformat` (STRICT: `force_unwrapping`/`force_try`/`force_cast` = **error**), a **required** CI job that **fails the build** on a violation (unlike FE's non-blocking lint), generated-client dir excluded, all hand-written code in scope | S | **done ✅ (via CI)** `8220f4c` (**#90**) — `.github/workflows/ios-ci.yml`: macOS, path-filtered to `src/cleansia_ios/**`, regenerates the Xcode projects, runs `swiftformat --lint` + `swiftlint lint --strict` as **BLOCKING** steps, then builds+tests CleansiaCore + both app schemes on a simulator | ios | T-0296✓ | — | **Phase 0** (early — gates every iOS ticket after) |
| **T-0324** | **Privacy manifest** — `PrivacyInfo.xcprivacy` per app target: required-reason-API audit (Keychain/auth/generated-client/UserDefaults), collected data types, **tracking=false**; assert no `AppTrackingTransparency`/ATT prompt anywhere | M | **proposed** | ios | T-0296, T-0300 | — | Phase 2 (after the auth/network surface exists to audit) |
| **T-0325** | **Purpose strings + Info.plist + entitlements** — `NSLocationWhenInUseUsageDescription`, `NSCameraUsageDescription`, `NSPhotoLibraryUsageDescription`(+Add) localized ×5; the **`aps-environment`** push entitlement; **no orphan permissions**, **no phantom push Info.plist key** | S | **proposed** | ios | T-0296 | — | Phase 2 (before the photo/map/push tickets ship their capability) |
| **T-0326** | **Sign in with Apple (customer app, 4.8)** — present SIWA alongside Google + email on the customer sign-in surface; working authenticated session | M | **proposed** (**gated on Q-IOS-04**) | ios | T-0312 | **Q-IOS-04 (owner): SIWA backend mechanism** — likely a backend `appleauth` endpoint + spec-regen | Phase 2 (customer; rides T-0312) |
| **T-0327** | **In-app account-deletion reachability (customer, 5.1.1(v))** — verify Settings → Delete Account reaches the `GdprDeletionService` account+data deletion **in-app** (not a web link/email/deactivation) | S | **proposed** | ios | T-0314 | — | Phase 2 (customer; verifies the T-0314 GDPR-delete surface) |
| **T-0328** | **External-payment / no-IAP documentation (3.1.3/3.1.5)** — record the citation that cleaning = real-world service → Stripe external payment is compliant + IAP must NOT be used; `SubscribePlus` framed as a real-world-service benefit; metadata framing note | S | **proposed** | ios, docs | T-0313 | — | Phase 2 (alongside the booking+Stripe ticket) |
| **T-0329** | **Pre-submission audit** — create + run `agents/backlog/ios-app-review-checklist.md`: every AR-* item per app (partner/customer) + the App-Store-Connect prerequisites (App Privacy answers, demo account, export-compliance, screenshots, age rating). **Release gate** — the first submission waits on it green | M | **proposed** | ios, qa, docs | T-0323..T-0328 | **owner: ASC submission prerequisites** (App Privacy, demo account, export-compliance, screenshots, signing) | **Pre-submission** (the final gate) |

### 10.3 Gate-AR — the standing per-ticket compliance gate (ADR-0016 D3)

In addition to the specific tickets above, **Gate-AR** runs on **EVERY** iOS ticket (reviewer + ios charters),
the way the ADR-0013/0014 reviewer checks #1–#13 do: (i) the **blocking lint/format gate is green**; (ii) any
**capability the ticket introduces carries its purpose string + privacy-manifest entry + locale strings in the
same ticket** (e.g. T-0308 photos adds the camera/photo purpose strings + manifest types in-ticket —
compliance is **not** deferred to T-0329); (iii) **no hidden feature / no private API / no placeholder** in the
ticket's surface; (iv) **VoiceOver/Dynamic Type** on new controls. This makes compliance **continuous** — the
pre-submission audit (T-0329) **confirms** an already-compliant app, it does not retrofit one.

### 10.4 Reviewer-check additions (ADR-0016 §"How a reviewer verifies", added to the iOS checks)

**#14** blocking SwiftLint/SwiftFormat gate exists + is green (`force_unwrapping`/`force_try`/`force_cast` =
error; generated dir excluded). **#15** privacy manifest per target (tracking=false; types match behavior); no
ATT/`AppTrackingTransparency`. **#16** purpose strings present + accurate ×5, no orphan permission, push =
`aps-environment` entitlement (no Info.plist push key). **#17** in-app account deletion reachable (customer).
**#18** SIWA on the customer sign-in (when Q-IOS-04 lands). **#19** no IAP for cleaning services; Stripe
external; the 3.1.3/3.1.5 citation on file. **#20** standard floor (no private API/hidden feature/placeholder;
demo account; functional+crash-free) + HIG/accessibility. **#21 (Gate-AR)** the ticket carried its own
purpose-string/manifest/locale compliance in-ticket. **#22 (Gate-DP, ADR-0018)** on every iOS **screen**
ticket: layout/flow/branding match the **cited** Android Compose screen (AR-DP-1); native SwiftUI components,
no Material re-impl, standard iOS patterns + affordances (AR-DP-2); Android↔iOS conflicts resolved
iOS-native, noted in-ticket, touching only the component (AR-DP-3).

### 10.6 Gate-DP — the standing per-screen design-parity gate (ADR-0018)

In addition to Gate-AR (§10.3), **Gate-DP** runs on **every iOS screen/feature ticket** (reviewer + ios
charters). The principle (ADR-0018, refining ADR-0013's "parity port"): **same layout/flow/branding as the
Android Compose apps, built with NATIVE SwiftUI components, and iOS convention WINS on a genuine component
conflict** (the "iOS component improvements" the owner asked for). The three checks are AR-DP-1/2/3, recorded
in §G of `ios-app-review-checklist.md`. **Pure-infra tickets are N/A** (T-0296 workspace, T-0298 DI, T-0300
auth, T-0301 spec, T-0302 codegen, T-0311 push-plumbing, T-0323 lint) — Gate-DP applies to the **screen**
tickets (T-0303, T-0304, T-0305, T-0306, T-0307, T-0308, T-0309, T-0310, T-0312, T-0313, T-0314). Each such
ticket **cites its Android Compose counterpart** and notes any iOS-native divergence.

### 10.5 Open question (ADR-0016)

- **Q-IOS-04** (`pre-submission` — gates **only** the SIWA ticket T-0326; owner + architect) — the **SIWA
  backend integration mechanism**. The 4.8 obligation is **confirmed**; the open input is whether SIWA needs a
  new backend **`appleauth`** anon endpoint (analogous to `googleauth` — a backend ticket + a spec-regen) or an
  existing exchange. **Default: assume a backend `appleauth` endpoint is needed** (the safe, `googleauth`-mirror
  assumption), gated on the owner confirming the backend appetite (it touches the auth contract). Does **not**
  block the rest of the iOS plan.

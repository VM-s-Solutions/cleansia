# Sprint 12 — iOS PORT (Wave 10): parity Swift/SwiftUI customer + partner apps

**Status:** 🎉 **PARTNER APP FEATURE-COMPLETE** (2026-06-28). PHASE 0 FOUNDATION DONE + MAC-VERIFIED + MERGED (2026-06-26) · **PHASE 1 (T-0303) DONE** — proving vertical green on `phase/ios-phase1` · **PHASE 2 — T-0304 + T-0305 DONE** on `phase/ios-phase2` · **PHASE 3 — T-0306 + T-0310 DONE** on `phase/ios-phase3` (merged #95) · **PHASE 4 — T-0307 (partner order work-loop, L→split, HARD AREA #3) + T-0308 (partner photo upload) DONE** on `phase/ios-phase4` (merged #96). **PHASE 5 — T-0309 (partner earnings + invoices + PeriodPay) + T-0311 (partner APNs push registration) DONE** on `phase/ios-phase5` (2026-06-28; **8 commits, pushed; Phase-5 PR drafted**) — **this phase makes the iOS PARTNER APP feature-complete** (every partner surface is now ported: auth → shell → dashboard → orders+photos → profile/devices/prefs → earnings/invoices → push). **T-0309:** 2 slices — A Earnings summary (reuses `getStats`) + PeriodPay (E1/E2 own-id) over the generated `PartnerEmployeePayrollAPI` + a Core `EarningsFormat`; the `.invoices` tab is now the Earnings surface (in-tab `NavigationStack`/`EarningsRoute`) / B invoices list+detail + the new Core `QuickLookPreview` seam (PDF) + the InvoicesStaleness silent-stale resume; `RefreshPhase` lifted to `CleansiaCore/State` (shared by Orders+Invoices). Reviewer **APPROVE**; **SECURITY PASS** (E1–E4 + TC-IOS-EARNINGS-OWNERSHIP; backend EmployeePayroll already JWT-scoped — **no T-0339-class gap**). **T-0311:** 2 slices — A the `PushRegistrar` Core seam + `PushTokenRegistrar` (`SessionScopedCache`) + the `Device/Register` client / B the `PushSessionObserver` (register on session×token) + the AppDelegate (`@UIApplicationDelegateAdaptor`) + the `Auth.setPreLogout` hook (logout unregisters BEFORE the token wipe) + the `aps-environment` entitlement + tap→OrderDetail. Reviewer **APPROVE**; **SECURITY PASS** (rules 1–4 + TC-IOS-PUSH-LOGOUT-CLEARS). End-to-end DELIVERY is OWNER-gated → **T-0342**. **PLUS a CI hardening:** ios-ci now runs the CleansiaPartner test suite (`build test`), not just `build` — the partner VM + security tests (366) now actually gate in CI for the first time. Tests: **CleansiaCore 194 + CleansiaPartner 366** (iPhone 17); swiftformat + swiftlint --strict clean. **T-0307:** 5 slices — A Core `SnapSheet` (ADR-0021 non-modal 3-snap) + `fullBleedMap` single-pin / B 3-pane OrdersList (sealed per-pane state + ported staleness cache) / C OrderDetail shell / D the OnTheWay lifecycle + the shared pure `OrderPrimaryAction` machine (**SECURITY PASS O1/O2/O4**) / E checklist/notes/issues/timeline; reviewer **APPROVE** all 5; security **PASS**. **T-0308:** 2 slices — A Core `CameraOrLibraryPicker` (first `UIViewControllerRepresentable`) + `ImageCompressor` (1920/0.7, EXPLICIT EXIF strip) / B `PhotosSection` (before/after rails, capture→upload) + Complete-unblock + bootstrapped `PrivacyInfo.xcprivacy` + camera/photo usage strings ×5; reviewer **APPROVE**; security **PASS** (P1–P5). Tests: **CleansiaCore 163 + CleansiaPartner 320** (iPhone 17); swiftformat + swiftlint --strict clean. **REQUIRED backend follow-up T-0339** (GetPagedOrders employeeId over-read — SECURITY, high, gates the GetPaged contract for go-live; iOS proceeded in parallel). · Phase 2+ tail (T-0309/0311/0312/0313/0314 + compliance T-0324…T-0329) proposed · follow-ups: **T-0334** (iOS ServiceAreaRow) / **T-0335** (iOS current-location FAB, gated on owner T-0325) / **T-0336** (iOS notifications-feed spike) / **T-0337** (Android partner profile sealed-state + i18n) / **T-0338** (Core catalog i18n ×5 + swappable bundle) / **T-0339** (backend GetPaged read-scoping — SECURITY) / **T-0340** (order-detail parity nits: checklist stable-id + Android status-label casing + placeholder-preview sweep); android i18n **T-0333** (Register/Forgot) prior · **owner items:** the camera/photo plist WORDING sign-off ×5 (T-0308 shipped en+cs/sk/uk/ru); T-0325 location string (owner, unused by Phase 4) · standing latent backend S8 (RefreshToken tenant read asymmetry the device-revoke kill rides on) tracked by **T-0236** (`done`) + `security/auth-sessions.md` + `security/ios-devices.md` — re-verify before any non-null-TenantId onboarding
**Created:** 2026-06-23
**Updated:** 2026-06-28
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

> **STATUS-LOG 2026-06-27 — PHASE 4 COMPLETE: T-0307 + T-0308 `proposed` → `done` on `phase/ios-phase4`
> (9 commits, pushed); Phase-4 PR drafted.** Both tickets passed the full workflow (ios dev → reviewer/Gate-DP
> + security on the touching slices). **T-0307 (partner order work-loop — OrdersList + OrderDetail + the OnTheWay
> lifecycle + checklist/notes/issues/timeline; `L → split` into 5 slices; HARD AREA #3) → `done`** —
> `4cb76ef` (ADR-0021 + the §7.8/§7.9 records) + `94050ae`+`3d0bf0d`+`7fca473`+`3c44356`+`42bb402`:
> **Slice A** = the Core `SnapSheet` (the **ADR-0021 non-modal 3-snap** custom `GeometryReader`+drag container, 16.0
> floor — NOT a modal `.sheet`) + the additive `MapProvider.fullBleedMap(coordinate:)` (single-pin `MKMapView`,
> camera-padded, no polygon param — §7.6 D1 additive seam); **Slice B** = the **3-pane OrdersList** (sealed
> per-pane `UiState<[OrderListItem]>` + a `RefreshPhase` enum + the **PORTED** per-pane staleness cache +
> the **Code→OrderStatus one-mapper** convention; Android E1 flag-bag NOT replicated → **T-0337**; SlideToCommit →
> native inline-confirm Gate-DP swap; **security O3** — the "mine" tab sends only the caller's own id, relies on the
> server for isolation); **Slice C** = the **OrderDetail shell** (the SnapSheet over the fullBleedMap + content
> cards); **Slice D** = the **OnTheWay lifecycle actions** (Take→NotifyOnTheWay→Start→Complete) + the **shared pure
> `OrderPrimaryAction.action(for:isMine:hasAfterPhotos:)` machine** (3 call sites, NOT inline switches) —
> **SECURITY PASS O1/O2/O4** (no client employeeId on any command; no-id-echo; clean reject + refresh + in-flight
> re-entry guard), **TC-IOS-ORDERS-OWNERSHIP** green; **Slice E** = the **checklist** (local checklist store) /
> **author-scoped notes + issues** / the **status timeline**. **Reviewer APPROVE on all 5 slices**; the SECURITY
> gate (§7.8, `security/ios-orders.md`, O1–O4) is **PASS** — the 10 state-changing/authorship-scoped backend
> command paths were traced on this Mac and are VERIFIED server-scoped + safe; the **one** gap is the **`GetPaged`
> read** (`GetPagedOrders.cs` trusts the client `Filter.EmployeeId` → leaks foreign-assigned coords/codes/pay,
> MEDIUM, reachable today) — a **pre-existing backend behavior**, NOT an iOS regression, filed as the **REQUIRED
> backend follow-up T-0339** (the iOS UI consumes the contract and proceeded in parallel). Deferred parity nits
> (checklist stable-id keying + Android status-label casing + the placeholder-preview literal sweep) → **T-0340**.
> **T-0308 (partner photo upload — camera/library capture → base64-over-JSON; 2 slices) → `done`** —
> `c216392` (the §7.10 photo records) + `cf6ea6d`+`a2a2184`: **Slice A** = the Core **`CameraOrLibraryPicker`**
> (the repo's **first `UIViewControllerRepresentable`**) + the **`ImageCompressor`** (downscale to 1920px / JPEG
> 0.7, with an **EXPLICIT, asserted EXIF/GPS strip** — the §7.10 D3/P3 design requirement, **TC-IOS-PHOTOS-EXIF-STRIP**
> green); **Slice B** = the **`PhotosSection`** (before/after rails, capture → upload) + the upload/delete VM +
> the **Complete-unblock** (after-photos gate the Complete action) + the bootstrapped **`PrivacyInfo.xcprivacy`**
> privacy manifest + the **NSCamera/NSPhotoLibrary usage strings ×5** (en + cs/sk/uk/ru). **Reviewer APPROVE**;
> the SECURITY gate (§7.10, `security/ios-photos.md`, P1–P5) is **PASS** — SavePhotos/DeletePhoto/GetPhotos
> ownership is VERIFIED safe on the reachable backend (**no backend change**: actor JWT-derived, DeletePhoto
> resolves ownership server-side photoId→order→caller, GetPhotos gates the whole read on `CanBrowseOrderAsync`,
> SAS URLs non-enumerable), **TC-IOS-PHOTOS-OWNERSHIP** green. **Tests:** **CleansiaCore 163 + CleansiaPartner 320**
> pass on the iPhone 17 simulator; **swiftformat + swiftlint --strict clean**; the **ios-ci** workflow (macOS,
> path-filtered) is the CI gate. **Owner items (PM never runs these):** (1) the **REQUIRED backend fix T-0339**
> (GetPaged read-scoping; gates the GetPaged contract for go-live); (2) the **camera/photo plist WORDING sign-off
> ×5** (T-0308 shipped en + cs/sk/uk/ru — owner may tweak the copy); (3) **T-0325** (the deferred location-permission
> purpose string ×5) remains owner-pending but is **unused by Phase 4** (the current-location FAB is deferred →
> T-0335). Resulting transitions: **T-0307 → `done`, T-0308 → `done`** (§3 + INDEX Wave-10 roster). The owner commits
> these backlog edits + opens the Phase-4 PR (the PM does not commit). Phase 2+ tail stays **proposed**.

> **STATUS-LOG 2026-06-28 — §7.11 SECURITY SUB-NOTE: T-0311 (APNs push registration) Gate-SEC ruling —
> security_touching YES · PASS-the-design (4 binding rules + 1 required test).** Logged ahead of the build on
> `phase/ios-phase5` (ARCHITECT rules the seam / lifecycle-home / foreground-permission flow in parallel; this
> sub-note rules only registration-authz / device-id / logout-clear / token-handling). Full record:
> `security/ios-push.md`. **security_touching = YES** — T-0311 is a **device-token write surface**
> (`/api/Device/Register`, `RegisterDeviceCommand{deviceId, deviceToken, platform="ios"}`, authed — NOT
> anon-allow-listed) plus the **logout-clear** is a load-bearing session-security property (a logged-out handset
> must STOP receiving pushes). **Greenfield on iOS** (`CleansiaCore/.../Push/Push.swift` = a bare placeholder
> enum, no register/unregister call site, no APNs delegate, no token cache on disk) → these are rules the dev
> builds to, NOT findings against shipped iOS code. **Backend traced on this Mac + VERIFIED safe** (no backend
> change): `RegisterDevice`/`UnregisterDevice` derive the user from the JWT session, bind the row to the caller,
> scope every lookup by `UserId`, reject empty sessions; a foreign `deviceId` cannot hijack/register to another
> account (per-`(UserId, DeviceId)` upsert + the composite unique index `DeviceConfiguration.cs:35`); Unregister
> soft-deletes caller-scoped (`Deactivate`→`IsActive=false`, never hard-remove —
> `UnregisterDeviceHandlerTests`), and that **stops APNs delivery** because the dispatcher fetches eligible rows
> via `GetByUserIdAsync` filtering `&& d.IsActive` (`SendPushNotificationHandler.cs:121` → `DeviceRepository.cs:30`,
> the S10 chain). S4 re-confirmed: register/unregister responses + `DeviceDto` carry **no `DeviceToken`** (the
> push secret stays server-side). **Four binding rules:** (1) **spine-authed register on the ONE device-id** —
> `deviceId` = `DeviceIdProvider.deviceId` (the same `X-Device-Id` mint-once source as T-0310 `deviceMine`; no
> 2nd id / `UUID()` / `identifierForVendor`), `platform=="ios"` literal; (2) **register on session×token**
> (the Android `PushTokenSessionObserver` parity — registration is a session-state property, never store/register
> unauthenticated); (3) **logout MUST `Device/Unregister` BEFORE `tokenStore.clear()`** (the Android
> `AuthRepository.kt:210-225` ordering — best-effort, but the authed DELETE needs the live Bearer or the row
> survives and pushes keep flowing post-logout) **AND** the last-token cache clears on **ALL** sign-outs by being
> a `SessionScopedCache` (so `Auth.signOutLocal()` `Auth.swift:189` + `SessionRefresher.forceSignOut()`
> `SessionRefresher.swift:76` both `clearAll()` it — closing the account-switch "next user inherits A's pushes"
> leak: A's row gone + A's token gone → B registers fresh); (4) **token handling (S6/S4)** — NO token logging
> anywhere incl. Sentry/crash (backend sweep clean), `UserDefaults` OK for the cache (device-scoped, not a user
> secret, reset-on-reinstall — NOT a location surviving `clearAll()`), no token in `Device/Mine` DTO (vetted
> T-0310). **Required test: TC-IOS-PUSH-LOGOUT-CLEARS (red-first)** — on logout, unregister is invoked BEFORE the
> token wipe (assert ordering / non-empty Bearer at the DELETE) AND the push cache `SessionScopedCache.clear()`
> runs on BOTH the explicit-logout and forced-signout paths. **No new backend follow-up**; standing latent S8
> (RefreshToken tenant read asymmetry, `auth-sessions.md`/`ios-devices.md`) re-verify before non-null-`TenantId`
> onboarding. Owner dependency = the **APNs auth key/cert** (infra, not a code gate; until set, the dispatcher
> `result.Skipped` no-op safely ACKs — `SendPushNotificationHandler.cs:149`). T-0311 stays **proposed** (ready to
> build on `phase/ios-phase5` against these rules).

> **STATUS-LOG 2026-06-28 — 🎉 PHASE 5 COMPLETE → the iOS PARTNER APP is FEATURE-COMPLETE. T-0309 + T-0311
> `proposed` → `done` on `phase/ios-phase5` (8 commits, pushed); Phase-5 PR drafted.** Both tickets passed the
> full workflow (ios dev → reviewer/Gate-DP + security on the touching slices). **This phase ports the last two
> partner surfaces (earnings/invoices + push), so every partner screen is now ported** — auth → shell → dashboard
> → orders+photos → profile/devices/prefs → earnings/invoices → push. **T-0309 (partner earnings + invoices +
> PeriodPay) → `done`** — `59be42b` (the §7.11/§7.12 decision docs) + `e4e7793` (Slice A) + `7daa412` (Slice B):
> **Slice A** = the **Earnings summary** (the `EarningsSummaryScreen.kt` parity, reuses the existing
> `PartnerDashboardClient.getStats` — §7.12 decision (d)) + **PeriodPay** (the per-period pay rollup) over the
> generated `PartnerEmployeePayrollAPI` (`employeePayrollGetPeriodPays`) on the ADR-0019 spine + a new Core
> **`EarningsFormat`** money/date helper (§7.12 decision (c) — the lift-the-dup-to-a-Core-helper idiom). **The
> `.invoices` shell tab is now the Earnings surface** — it roots an in-tab `NavigationStack` over a typed
> `EarningsRoute` enum landing on the summary (§7.12 decision (a), the §7.7 D1 in-tab-stack precedent), and the
> Dashboard's `onOpenEarnings` is now a tab-switch (the `selectOrders()` parity). **Slice B** = the **invoices
> list** (`employeePayrollGetPagedInvoices`, sealed `UiState<[EmployeeInvoiceDto]>`) + **detail**
> (`employeePayrollGetInvoiceById`) + **PDF viewing** via the new Core **`QuickLookPreview`** seam (§7.12 decision
> (b) — the T-0308 §7.10 D1 system-UIKit-controller-behind-a-Core-seam idiom, applied to QLPreviewController, over
> `employeePayrollDownloadInvoice`'s local file URL) + the **InvoicesStaleness** silent-stale resume (the ported
> `getMyInvoicesStaleness()`/`invalidateMyInvoices()` watermark). **`RefreshPhase` was LIFTED to
> `CleansiaCore/State`** (was Orders-local in T-0307; now shared by Orders + Invoices — the 2nd consumer triggers
> the harvest). **Reviewer APPROVE**; the **SECURITY gate (§7.11, `security/ios-earnings.md`, E1–E4) is PASS** —
> and the headline: **UNLIKE `GetPagedOrders`/T-0339, all four EmployeePayroll handlers ALREADY pin to the JWT
> caller** for non-admins (`GetPeriodPays`/`GetPagedInvoices`/`GetInvoiceById`/`DownloadInvoice` traced + the
> existing `GetPeriodPaysOwnershipTests` green 4/4), so there is **NO T-0339-class backend over-read and NO new
> backend follow-up**; E1 own-server-derived id only, E2 no foreign-id echo, E3 download-own-only, **E4 the
> PII-bearing PDF is deleted from cache on preview-dismiss** (TC-IOS-EARNINGS-OWNERSHIP green). The latent S5
> payroll-route rate-limit gap is folded into **BSP-4d** (backend hardening, not a T-0309 blocker). The Android E1
> invoices flag-bag is NOT replicated (iOS born sealed-state) → **T-0337**. **T-0311 (partner APNs push
> REGISTRATION + token plumbing + device lifecycle + minimal foreground/tap) → `done`** — `f2a999f` (the
> §7.11/§7.13 decision docs + the T-0342 owner ticket) + `8d53b18` (Slice A) + `b4fb556` (Slice B): **Slice A** =
> the Core **`PushRegistrar`** seam (the SOLE `UNUserNotificationCenter`/`registerForRemoteNotifications` consumer
> — the ADR-0014 D6′/ADR-0018 D2 system-framework-behind-a-Core-seam family) + the **`PushTokenRegistrar`**
> (cache-short-circuit + persist-on-success; the last-registered-token store is a **`SessionScopedCache`**) + the
> `Device/Register` client over the generated `PartnerDeviceAPI` on the ADR-0019 spine (`deviceId` == the one
> `DeviceIdProvider`, `platform == "ios"`). **Slice B** = the Core **`PushSessionObserver`** (the
> `PushTokenSessionObserver.kt` `combine(session, token)` parity — registration is a session×token property, never
> an event hook) + the per-app **`@UIApplicationDelegateAdaptor`** AppDelegate (feeds the APNs-token stream;
> `willPresent` foreground banner + `didReceive` tap → existing OrderDetail route via the
> `PartnerNotificationDeepLink` port) + the **`Auth.setPreLogout` hook** (logout invokes `Device/Unregister`
> **BEFORE** the `TokenStore` wipe so the authed DELETE has a live Bearer; the cache `clear()` rides every
> sign-out as a `SessionScopedCache`) + the **`aps-environment` entitlement**. **Reviewer APPROVE**; the
> **SECURITY gate (§7.11, `security/ios-push.md`, rules 1–4) is PASS** — verified vs code: (1) spine-authed
> register on the ONE device-id, (2) register on session×token, (3) **the `setPreLogout` hook unregisters before
> the token wipe AND the push cache clears on ALL sign-outs** (explicit-logout + forced-signout), (4) no token
> logging / `UserDefaults` cache OK / no token in any DTO; **the new Auth-spine `setPreLogout` hook is safe +
> non-regressing** (best-effort, does not block sign-out, no new anon entry); **TC-IOS-PUSH-LOGOUT-CLEARS** green;
> **no new backend follow-up** (`RegisterDevice`/`UnregisterDevice` already JWT-scoped + soft-delete stops APNs
> delivery via the `&& d.IsActive` dispatcher filter). **End-to-end push DELIVERY is OWNER-gated → T-0342** (the
> APNs `.p8` auth key + the Push Notifications capability/provisioning on the App ID): T-0311 ships
> **code-complete + the `aps-environment` entitlement WITHOUT it** (the T-0325-gates-T-0335 pattern; until the key
> is set the dispatcher `result.Skipped` no-ops safely). **CI hardening (call out):** the `1eb346f` commit changes
> the **ios-ci** workflow's partner step from `build` to **`build test`** — so the CleansiaPartner VM + security
> test suite (**366 tests**, incl. the iOS push/orders/photos/devices ownership tests) now **actually gate in CI
> for the first time** (previously only CleansiaCore's suite + the partner *build* gated). This is a real gate
> hardening, not a cosmetic change. **Tests:** **CleansiaCore 194 + CleansiaPartner 366** pass on the iPhone 17
> simulator; **swiftformat + swiftlint --strict clean**. **Gate-DP divergences (recorded, all
> component/mechanism — none touch layout/flow/branding):** Coil-family card visuals → native SwiftUI +
> `AsyncImage`/the QuickLook PDF swap (Android FileProvider/`ACTION_VIEW` → iOS QLPreviewController); FCM →
> APNs over the **same** `Device/*` contract (ADR-0013 D8); the Android push+tab two-surface nav →
> single-tab + in-tab `NavigationStack`; the Android E1 invoices flag-bag NOT replicated → **T-0337**. **Owner /
> manual steps (PM never runs these):** (1) **T-0342** — the APNs `.p8` auth key + the Push capability/provisioning
> (the end-to-end-delivery gate; code ships without it); (2) the **camera/photo plist WORDING sign-off ×5**
> (carried from T-0308, still pending owner); (3) **T-0325** — the deferred location-permission purpose string ×5
> (owner, **unused this phase** — the current-location FAB is T-0335). **T-0339 reconciliation (RESOLVED — it IS in
> master):** T-0339 (the backend `GetPagedOrders` read-scoping SECURITY fix) landed in master via the PR #96
> **SQUASH-merge**. PR #96 has a single parent (`7055ef4`), so the original commit `d688d30` is not a master
> *ancestor* (a squash flattens the originals — which is why `git merge-base --is-ancestor d688d30 master`
> misreads NO); but master's TREE contains the fix — `GetPagedOrdersScopeIntegrationTests.cs` +
> `RestrictToEmployeeId` (OrderSpecification) + the GetPagedOrders caller-pin are all present in `origin/master`
> (verified by tree content 2026-06-28). T-0339 → **done**; no owner action needed. Resulting transitions: **T-0309 → `done`, T-0311 → `done`** (§3 + INDEX Wave-10
> roster); **the iOS partner app is feature-complete.** Next: the customer app batch (T-0312…T-0314) is the
> remaining Wave-10 scope; the partner follow-ups (T-0334–T-0342) are filed. The owner commits these backlog edits
> + opens the Phase-5 PR (the PM does not commit).

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
| **T-0307** | **Partner order work-loop** — OrdersList + OrderDetail (full-bleed map + 3-snap sheet) + the **OnTheWay** lifecycle (Take→NotifyOnTheWay→Start→Complete) + checklist/notes/issues/timeline. **Acceptance scope + the 5 Understand-pass rulings fixed in §7.9 (architect) + the SECURITY order-action gate in §7.8:** (a) the additive `MapProvider.fullBleedMap(coordinate:)` — single pin, camera-padded, NO polygon param (no polygon data in spec; §7.6 D1 additive seam); (b) the **non-modal `SnapSheet` 16.0-floor sheet = ADR-0021** (custom `GeometryReader`+drag container, NOT a modal `.sheet`; the floor stays 16.0); (c) the pure shared `OrderPrimaryAction.action(for:isMine:hasAfterPhotos:)` machine (3 call sites, NOT inline switches; ownership trust = §7.8); (d) the T-0308 photo precursor seam (disabled/placeholder Photos slot + `hasAfterPhotos` consumer; capture additive in T-0308); (e) sealed per-pane `UiState<[OrderListItem]>` + a `RefreshPhase` enum + **PORTED** per-pane staleness cache (Android E1 flag-bag NOT replicated→T-0337; SlideToCommit→native confirm Gate-DP swap). + the Code→OrderStatus one-mapper convention. Reviewer #29/#30/#31 + Gate-DP + TC-IOS-SNAP/-ORDER-ACTION; **SECURITY gate** (§7.8, O1–O4 + the backend GetPaged read-scoping fix). **Android parity: `partner-app/.../features/orders/{OrdersListScreen,OrdersListViewModel,OrderDetailScreen,OrderDetailViewModel,OrderPrimaryAction,CleaningChecklist,StatusTimeline,OrderStatusPill,PhotosSection}.kt` + `data/orders/OrdersRepository.kt`** | **L → split** | **done ✅** `4cb76ef`+`94050ae`+`3d0bf0d`+`7fca473`+`3c44356`+`42bb402` (`phase/ios-phase4`; **5 slices** — A Core `SnapSheet` (ADR-0021 non-modal 3-snap) + `fullBleedMap(coordinate:)` single-pin `MKMapView`; B 3-pane OrdersList (sealed per-pane `UiState`+`RefreshPhase`+ported staleness cache + Code→OrderStatus one-mapper; security O3 own-id-only); C OrderDetail shell (SnapSheet over fullBleedMap + content cards); D the OnTheWay lifecycle actions + the shared pure `OrderPrimaryAction.action(for:isMine:hasAfterPhotos:)` machine — **SECURITY PASS O1/O2/O4**, TC-IOS-ORDERS-OWNERSHIP; E checklist (local store)/notes+issues (author-scoped)/timeline. Reviewer **APPROVE** all 5; **SECURITY PASS** (§7.8 O1–O4; the one backend GetPaged D2b gap → **T-0339**, iOS proceeds in parallel). E1 flag-bag NOT replicated→T-0337; SlideToCommit→native confirm Gate-DP swap; deferred checklist stable-id→T-0340; current-location FAB→T-0335. CleansiaCore 163 + CleansiaPartner 320 pass on iPhone 17 sim; swiftformat/swiftlint --strict clean) | ios | T-0304✓, T-0306✓ | — | 4 (**HARD AREA #3**; `phase/ios-phase4`) |
| **T-0308** | **Partner photo upload** — camera capture → **JSON base64** photos (partner shape) on OrderDetail | M | **done ✅** `c216392`+`cf6ea6d`+`a2a2184` (`phase/ios-phase4`; **2 slices** — A Core `CameraOrLibraryPicker` (the repo's first `UIViewControllerRepresentable`) + `ImageCompressor` (1920/0.7, **EXPLICIT EXIF strip**, TC-IOS-PHOTOS-EXIF-STRIP); B the `PhotosSection` (before/after rails, capture→upload) + the upload/delete VM + the Complete-unblock + the bootstrapped `PrivacyInfo.xcprivacy` + the NSCamera/NSPhotoLibrary usage strings ×5. Reviewer **APPROVE**; **SECURITY PASS** (§7.10 P1–P5, TC-IOS-PHOTOS-OWNERSHIP — backend SavePhotos/DeletePhoto/GetPhotos ownership VERIFIED safe, no backend change). Owner sign-off pending on the camera/photo plist WORDING ×5) | ios | T-0307✓ | **owner: camera/photo plist WORDING sign-off ×5** | 4 (HARD AREA #3 cont.; `phase/ios-phase4`) |
| **T-0309** | Partner earnings + invoices + PeriodPay (`EmployeePayroll/GetPeriodPays` — a regen'd-spec endpoint). **Acceptance scope + the 4 Understand-pass rulings fixed in §7.12** (architect — nav/PDF-seam/format/stats-source) + the **§7.11 SECURITY read-scoping gate** (E1–E4) | M | **done ✅** `59be42b`+`e4e7793`+`7daa412` (`phase/ios-phase5`; **2 slices** — A Earnings summary (reuses `PartnerDashboardClient.getStats`, §7.12 (d)) + PeriodPay over the generated `PartnerEmployeePayrollAPI` (ADR-0019 spine) + a Core `EarningsFormat` helper; the `.invoices` tab roots an in-tab `NavigationStack`/`EarningsRoute` landing on the summary (§7.12 (a)); `onOpenEarnings` is now a tab-switch. B invoices list (`getPagedInvoices`, sealed `UiState`) + detail (`getInvoiceById`) + PDF via the new Core **`QuickLookPreview`** seam (§7.12 (b)) over `downloadInvoice` + the ported **InvoicesStaleness** silent-stale resume; **`RefreshPhase` LIFTED to `CleansiaCore/State`** (now shared by Orders+Invoices). Reviewer **APPROVE**; **SECURITY PASS** (§7.11 E1–E4, TC-IOS-EARNINGS-OWNERSHIP — backend EmployeePayroll already JWT-scoped for non-admins, `GetPeriodPaysOwnershipTests` green 4/4, **NO T-0339-class gap, NO backend follow-up**; E4 PDF deleted from cache on dismiss; latent S5 rate-limit → BSP-4d). Android E1 invoices flag-bag NOT replicated → T-0337) | ios | T-0304✓ | — | 5 (partner; `phase/ios-phase5`) |
| **T-0310** | Partner **Profile tab** (replaces `PartnerShellView.swift:36` `PlaceholderTab`) — the hub (hero + contract-status chip + section-group rows + logout) + **6 section editors** (Personal/Address/Identification/Bank/Emergency/Documents) over a new `PartnerProfileClient` (ADR-0019 spine) + the **onboarding chain** + **Devices** (Device/Mine list + revoke — **SECURITY-ruled, decisions 6–8**) + **Preferences** (Language/Theme). **Acceptance scope + the 5 Understand-pass rulings fixed in §7.7:** D1 (in-tab `NavigationStack` over a typed `ProfileRoute` enum — ADR-0020 intra-audience push, reviewer #28a), D2 (**the load-bearing call** — the RegistrationLock owns its OWN local `NavigationStack` + chain VM and pushes the SHARED section set over itself with `onboarding == true`; fail-closed, no cross-audience shell routing — reviewer #28b + TC-IOS-LOCK-CHAIN, composes with #24), D3 (`ServiceAreaRow` DEFERRED → T-0334, a Gate-DP divergence; Address ships pan/search/save at parity), D4 (EXTEND the one `AppSettingsStore` with writable language + a Theme enum + setters; honor theme via `.preferredColorScheme` now — reviewer #28c), D5 (born sealed-state canonical — Android E1 flag-bags NOT replicated; android fix → T-0337; reviewer #28d). **Scope cuts (PM to record):** current-location FAB + `LocationProvider` seam DEFERRED → T-0335 (gated on T-0325); **"Notifications" DROPPED** (no Android prefs surface / no backend prefs API / no client; the in-app feed is a separate spike → T-0336). Reviewer #28 + TC-IOS-PROFILE-ROUTE/-LOCK-CHAIN/-SECTION-SHARED/-SETTINGS-THEME/-PROFILE-STATE. Gate-DP. **Android parity: `partner-app/.../features/profile/` (`ProfileScreen.kt`/`ProfileViewModel.kt`, the `*Section*` set, `OnboardingChainHeader.kt`, `SectionScaffold.kt`, `AddressSectionScreen.kt`) + `features/orders/{RegistrationLockViewModel,OnboardingChainViewModel}.kt` + `core/settings/AppSettingsRepository.kt`** | M | **done ✅** `ce6c5fc`+`ee2f044`+`2cdaf93`+`6c6155c` (`phase/ios-phase3`; 3 slices. Slice A = the profile hub + 6 section editors (Personal/Address/Identification/Bank/Emergency/Documents) + onboarding chain + the now-live RegistrationLock Fix-CTAs (D2: the lock owns its OWN `NavigationStack`+chain VM, pushes the SHARED section set with `onboarding==true`, fail-CLOSED — gate #24 byte-unchanged, verified). Slice B = Devices (Device/Mine list + revoke) — **SECURITY PASS** on all binding rules (D6 single device-id source, D7a hide-on-current + D7b defensive self-revoke sign-out, D8 server-scoped revoke verified vs the backend; TC-IOS-DEVICES-SELF-REVOKE green). Slice C = Preferences (language [+ a System/follow-device row] + theme pickers; theme honored via `.preferredColorScheme`; the first runtime in-app language switch). D3 `ServiceAreaRow` DEFERRED → T-0334; D5 born sealed-state, Android E1 flag-bags NOT replicated → T-0337; current-location FAB → T-0335; Notifications DROPPED → T-0336. Reviewer **APPROVE** (incl. a re-review of the System-row fix); **185 CleansiaPartner tests**; swiftformat/swiftlint clean) | ios | T-0304✓, T-0306✓ | — | 2 (partner) |
| **T-0311** | **Push (APNs) — REGISTRATION + token plumbing + device lifecycle + minimal foreground/tap** (NOT the in-app feed → T-0336) — register for remote notifications → APNs token + `Platform="ios"` + same `X-Device-Id` to `/api/Device/*` (generated `PartnerDeviceAPI` on the ADR-0019 spine); re-register on login, clear on logout (the `:core` push parity). **Acceptance scope + the 3 Understand-pass rulings fixed in §7.13 (no new ADR):** (a) a Core **`PushRegistrar`** seam (the SOLE `UNUserNotificationCenter`/`registerForRemoteNotifications` consumer — the ADR-0014 D6′/ADR-0018 D2 seam-family) + a per-app **`@UIApplicationDelegateAdaptor`** feeding its APNs-token stream (reviewer #34a); (b) **the load-bearing call** — a Core **`PushSessionObserver`** (the `PushTokenSessionObserver.kt` `combine(session,token)` parity), `unregisterDevice()` invoked from `AuthApiClient.logout()` BEFORE the `TokenStore` wipe + local `clear()` via the `SessionScopedCacheRegistry` (reviewer #34b; the **ordering GATE = SECURITY/Gate-SEC**); (c) minimal `willPresent`+`didReceive`-tap (→ existing order route via a `PartnerNotificationDeepLink` port), in-app feed→T-0336, **no plist key** (only the `aps-environment` entitlement), skip the rationale string, **no `UiState`/`ActionState`** (correct — §7.6 D3 precedent) (reviewer #34c). **2 slices** (A = `PushRegistrar` seam + `Device/Register` client + registrar logic; B = `PushSessionObserver` wiring + `@UIApplicationDelegateAdaptor` + foreground/tap + the entitlement). FCM→APNs over the same `Device/*` contract = the recorded Gate-DP divergence (ADR-0013 D8). TC-IOS-PUSH-REGISTER/-OBSERVER/-LOGOUT-ORDER/-TAP. **Android parity: `core/.../notifications/{PushTokenRepository,PushTokenSessionObserver,DeviceRegistrationClient,PushTokenDataStore}.kt` + `partner-app/.../data/auth/AuthRepository.kt:210-231`** | M | **done ✅** `f2a999f`+`8d53b18`+`b4fb556` (`phase/ios-phase5`; **2 slices** — A the Core `PushRegistrar` seam (the SOLE `UNUserNotificationCenter`/`registerForRemoteNotifications` consumer) + `PushTokenRegistrar` (cache-short-circuit + persist-on-success; the last-token store is a `SessionScopedCache`) + the `Device/Register` client (generated `PartnerDeviceAPI`, ADR-0019 spine; `deviceId`==the one `DeviceIdProvider`, `platform=="ios"`). B the Core `PushSessionObserver` (`combine(session,token)` parity — registration is session×token state) + the per-app `@UIApplicationDelegateAdaptor` AppDelegate (`willPresent` banner + `didReceive` tap → OrderDetail via the `PartnerNotificationDeepLink` port) + the new **`Auth.setPreLogout` hook** (logout `Device/Unregister`s BEFORE the `TokenStore` wipe) + the `aps-environment` entitlement. Reviewer **APPROVE**; **SECURITY PASS** (§7.11 `security/ios-push.md` rules 1–4 + TC-IOS-PUSH-LOGOUT-CLEARS — verified vs code; the `setPreLogout` hook safe/non-regressing; no token in any DTO/log; no T-0339-class backend gap — `RegisterDevice`/`UnregisterDevice` JWT-scoped + soft-delete stops APNs delivery). FCM→APNs over the SAME `Device/*` contract = the recorded Gate-DP divergence (ADR-0013 D8). **End-to-end DELIVERY is OWNER-gated → T-0342** (code ships complete + the entitlement without the `.p8` key). **CI hardening:** ios-ci's partner step is now `build test` (the 366 partner tests gate for the first time — `1eb346f`)) | ios | T-0302✓, T-0303✓, T-0310✓, T-0331 | **owner: T-0342 (APNs `.p8` key + Push capability/provisioning) — the end-to-end-DELIVERY gate; T-0311 ships code-complete + the `aps-environment` entitlement without it (the T-0325-gates-T-0335 pattern)** | 5 (cross-app; `phase/ios-phase5`) |
| **T-0312** | **Customer app shell SCAFFOLD + FULL auth** (the FIRST customer feature) — `CustomerRootView` flat-enum root-switch (the ADR-0020 *pattern*, copied) splash-gated, **NO RegistrationLock** (the simpler customer gate) → `CustomerShellView` (4-tab `TabView` Home·Orders·Rewards·Profile + center **Book FAB**, tabs as placeholders, FAB present-but-INERT) + the FULL auth surface (SignIn/SignUp/EmailVerify/ForgotPassword + the event-driven `CustomerAuthViewModel` + **Google via GoogleSignIn-iOS + Apple via `ASAuthorizationAppleIDButton`/SIWA**). **Acceptance scope + the 7 Understand-pass rulings fixed in §7.15 (no new ADR):** D1 (the `CustomerRootView` PATTERN-copy of ADR-0020 D5, no registration gate — Android customer `CleansiaNavHost.kt:137-147` parity), D2 (the T-0305 chain + the Android `AuthViewModel` event contract + SIWA-above-Google below the `OR` divider, AR-ACCT-2/4.8), D3 (provider-ACQUISITION app-local in `CleansiaCustomer`; provider-CONSUMPTION = 2 new `AuthApiClient` methods reusing the shared empty-token gate + the single Keychain mutation; `+/api/auth/appleauth` to the allow-list), D4 (the T-0304 shell-scaffold deferral map — Book FAB action → T-0313, tab content + soft onboarding → T-0314, LIVE social → owner-gated), D5 (build shell+root+acquisition+VMs AHEAD of the regen against a `SocialSignInProviding` protocol + fakes; bind the live AppleAuth POST after T-0343+regen), D6 (Gate-DP: pager+floating-pill → native `TabView`+FAB-overlay, official SIWA button, GoogleSignIn-iOS SPM dep + the `com.apple.developer.applesignin` entitlement in `project.yml`), D7 (3 slices A scaffold / B auth-core / C social; **B+C SECURITY-TOUCHING → Gate-SEC parallel**). TC-IOS-CUSTOMER-ROUTER-SEED/-SPLASH-RESOLVE/-SHELL/-AUTH-OUTCOME/-EMPTYTOKEN/-VERIFY-EMAIL-ARG/-ANON + TC-IOS-SOCIAL-OUTCOME/-NONCE. Gate-DP + Gate-AR. **Android parity: customer `features/main/MainShell.kt`, `navigation/CleansiaNavHost.kt`, `features/auth/{AuthViewModel,SignInScreen,SignUpScreen,EmailVerifyScreen}.kt`, `core/auth/GoogleSignInController.kt`.** | M (**3 slices**) | **proposed** | ios | T-0302✓, T-0306✓ | **owner: rides the §7.14 regen of `customer-mobile-api.json` (`AppleAuth`, T-0343) + T-0344 (Apple capability/`Apple:BundleId`) + T-0345 (Google client ids/IMP-1) — code ships complete + the entitlement/SPM dep; LIVE social sign-in is owner-gated (the T-0311-gated-by-T-0342 pattern)** | 2 (customer; the first customer wave) |
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
2. **APNs auth key (`.p8`) + Push capability/provisioning (Apple Developer)** — for T-0311 push (the Android
   `google-services.json` analogue). Owner provisioning; flagged, not built by agents. **Ticketized as
   T-0342** *(NOT "T-0341", which is the backend status-history flaky-test ticket)* — the
   **end-to-end-DELIVERY** gate: T-0311 ships code-complete + the `aps-environment` entitlement without it
   (the T-0325-gates-T-0335 pattern; §7.13).
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

### 7.10 T-0308 (Phase-4 partner photo upload/delete on OrderDetail) — SECURITY gate ruling (recorded 2026-06-27, security reviewer; Gate-SEC)

> **STATUS-LOG 2026-06-27 — T-0308 SECURITY GATE: partner photo upload/delete/read surface —
> `security_touching: YES`; verdict = PASS-the-iOS-design (binding client rules P1-P5) + ONE design
> requirement (P3 explicit EXIF strip). The backend SavePhotos/DeletePhoto/GetPhotos ownership scoping
> is VERIFIED safe on the reachable backend — NO backend CHANGES. Photo analogue of the T-0307
> order-action gate (§7.8). Full S1-S10 walk + binding rules in `security/ios-photos.md`.**
>
> **Greenfield client, like T-0307/Devices:** `phase/ios-phase4` carries T-0307 order code + the
> **generated** photo DTOs (`CleansiaPartnerApi/Models/SaveOrderPhotos*.swift`,
> `DeleteOrderPhotoResponse.swift`, `GetOrderPhotos*.swift`, `PhotoType.swift`) but **no iOS
> capture/image-picker/UIImage/camera code on disk** (tree grep clean). So P1-P5 are rules the
> developer builds to, not findings against shipped iOS code. The **backend photo surface was traced on
> this Mac** (same discipline as T-0307 §7.8 / Devices D8).
>
> **D2 — upload/delete/read ownership (S1/S2/S3) — VERIFIED SAFE (no backend gap, unlike GetPaged).**
> Actor is JWT-derived (`OrderAccessService.GetCallerEmployeeIdAsync`; commands carry NO client
> employeeId — SavePhotos=`{orderId,photos[]}`, DeletePhoto=`{photoId}` only; `capturedByEmployeeId` +
> `capturedAt` are server-stamped, `SaveOrderPhotos.cs:139`/`OrderPhoto.cs:82`). **SavePhotos** scopes
> by `order.AssignedEmployees.Any(oe=>oe.EmployeeId==employeeId)` (`SaveOrderPhotos.cs:101-104`;
> `UploadOrderPhoto.cs:90-93`). **DeletePhoto resolves ownership SERVER-SIDE** photoId→order→caller
> (`GetByIdAsync(photoId)` → load `photo.OrderId`'s order → assignment check;
> `DeleteOrderPhoto.cs:39,51-59`) — the client cannot name the owner; foreign-order delete →
> `EmployeeNotAssignedToOrder`. **GetPhotos** gates the whole read on `CanBrowseOrderAsync`
> (`GetOrderPhotos.cs:57-62`; assigned/customer/admin/available-spots) — no photoId-keyed read, no
> guess-a-photoId path; foreign no-spots order → `OrderNotFound` (existence hidden). All VERIFIED.
>
> **D3 — EXIF/GPS (S4-adjacent) — DESIGN REQUIREMENT P3.** The backend stores uploaded bytes verbatim
> to blob (no server EXIF scrub, `SaveOrderPhotos.cs:123-127`), so camera GPS/EXIF survives unless the
> CLIENT strips it. `UIImage.jpegData` drops EXIF as a *side effect* — but that incidental coupling is
> fragile (a future metadata-preserving encode silently re-leaks the cleaner's/customer's precise GPS).
> **RULING: EXIF/GPS strip must be an EXPLICIT, asserted invariant of the upload boundary, not an
> incidental side-effect** — single re-encode chokepoint + a test asserting the produced JPEG has no
> `kCGImagePropertyGPSDictionary`. The photo analogue of "never upload the raw camera asset."
>
> **D4 — permission-denial UX + bounds — PASS.** Denied camera/library degrades cleanly (clear message
> + Settings deep-link, never a silent dead control; `Info.plist` declares
> `NSCameraUsageDescription`/`NSPhotoLibraryUsageDescription` — manifest manual step). **Size cap
> (S5):** server enforces 10 MB/photo (`SaveOrderPhotos.cs:37,60-64`) so a 50 MB body is rejected;
> client downscales before encode (P5). **Rate limit (S5):** SavePhotos/UploadPhoto/DeletePhoto carry
> `[EnableRateLimiting("auth")]` (partitioned per-JWT-sub, `OrderController.cs:112,125,150`). **SAS
> (S4) non-enumerable:** GetPhotos returns per-blob, 1h-expiry, Read-only SAS URLs
> (`BlobContainerClient.cs:86-113`) over blob names with a random GUID — a partner cannot guess/forge
> another order's photos (and never gets a foreign list). **S6/S8/S10 PASS** (no PII logged; `OrderPhoto
> : ITenantEntity` global-filtered through `GetDbSet`; hard-delete, no soft-delete leak). **S9 N/A** (no
> schema/DTO change — DTOs already generated; T-0308 = client capture code only).
>
> **Binding client rules (reviewer enforces):** **P1** no client actor field on SavePhotos/DeletePhoto
> (orderId/photoId only; actor=JWT). **P2** no-id-echo — upload only to the loaded `orderId`, delete
> only a `photoId` from this order's own `getPhotos` response. **P3** explicit, asserted EXIF/GPS strip.
> **P4** clean permission-denial + Info.plist usage strings. **P5** client downscale before encode +
> re-entry-guard the upload while a call is in flight (no double-tap duplicate). Client rail-gating
> (canUpload by status) is UI-only — the server is authority (the `completeBlocked` precedent).
> **Required tests:** **TC-IOS-PHOTOS-OWNERSHIP** (client VM: SavePhotos from own orderId only +
> DeletePhoto from a loaded photoId only + clean reject/refresh + upload re-entry guard) +
> **TC-IOS-PHOTOS-EXIF-STRIP** (assert the encoded JPEG has no GPS dictionary — gates P3).
>
> **Owner follow-ups (NONE block T-0308; all LOW/latent):** (1) DeletePhoto authorship — any assigned
> cleaner can delete another's photo on shared jobs (`MaxEmployees>1`); add `capturedByEmployeeId`
> author check before shared jobs, dormant today. (2) cap photos-per-SavePhotos-request (e.g. ≤10) so
> the 10 MB/photo cap can't be multiplied. (3) optional server-side EXIF scrub as defense-in-depth
> behind the client P3 strip.

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

### 7.10 T-0308 (Phase-4 partner order PHOTOS — camera/library capture → base64-over-JSON upload, read-back, delete, Complete-unblock) — acceptance scope + the four Understand-pass rulings (recorded 2026-06-27, architect)

> **The photo-upload OWNERSHIP / EXIF-gate is ruled by SECURITY in parallel (`security/ios-orders.md`) — this
> architect record stays OUT of it.** The decisions below rule only the *capture seam shape*, the *compression
> target*, the *read-back/image-loading component*, and the *plist scope* — never who may upload to which order,
> nor metadata stripping. T-0307's §7.9 (d) already CONFIRMED the photo precursor seam (the reserved Photos slot +
> the `hasAfterPhotos` consumer); T-0308 makes the producer real.

T-0308 (`L → split`, `phase/ios-phase4`, depends_on T-0307) fills the disabled Photos placeholder T-0307 left in
the OrderDetail sheet (gated behind `showWorkSections`), via camera/library capture → **base64-over-JSON** upload —
the **partner** shape: `PartnerOrderAPI.orderSavePhotos(SaveOrderPhotosCommand{orderId, photos:[SaveOrderPhotos
PhotoToSave{photoType, file: BlobFileDto{fileName, base64Content, contentType}, notes}]})`; read-back via
`orderGetPhotos(orderId) → GetOrderPhotosResponse{photos:[GetOrderPhotosOrderPhotoDto{id, blobUrl, photoType…}]}`;
delete via `orderDeletePhoto(photoId)`. `PhotoType._1 = Before`, `._2 = After`. The after-photo flips
`OrderPrimaryAction.completeBlocked → .complete` via the server-recomputed `OrderItem.hasAfterPhotos` (T-0307 §7.9
(c) built the consumer; T-0308 makes the producer real). **2 slices (§6):** A = the capture seam + the base64/
compression util (Core); B = `PhotosSection` + the upload/delete VM + the Complete-unblock wiring.

**Android parity source (the iOS port mirrors it):** `partner-app/.../features/orders/PhotosSection.kt` (two rails
Before/After, each an Add tile + thumbnail strip + per-tile delete), `OrderPhotosViewModel.kt` (the
`OrderPhotosUiState` Loading/Error/Loaded + the `PhotoMutationState{isUploading, deletingId}` + the
`mutationVersion` monotonic counter → parent refresh), `OrderDetailScreen.kt` (the `PhotosSection` wiring +
`canUploadBefore/After` windows + `canComplete = order.hasAfterPhotos == true`, `:530-558`), and
`data/orders/OrdersRepository.kt` (`getPhotos`/`uploadPhoto`/`deletePhoto`, the single-photo `orderSavePhotos`
batch-of-one, `:261-297`). **Gate-DP applies** (T-0308 is a screen ticket): the Photos section cites
`PhotosSection.kt`; native SwiftUI; iOS-wins-on-conflict + the noted divergences (camera-vs-gallery, the
1920/0.7 compression, Coil → `AsyncImage`).

**This is a "record, not ADR" ruling — the §7.2/§7.4/§7.5/§7.6/§7.7/§7.9 precedent.** All four decisions **APPLY
accepted ADRs**: decision (a) applies ADR-0018 D2 (native-SwiftUI brand-skin component) + D3/Gate-DP (the noted
camera enhancement divergence); (b) applies the Parity rule + ADR-0018 D3 (a recorded pixel-dimension divergence
for a perf reason) — a bounded pure helper, no optimizer pass needed; (c) applies ADR-0018 D3 (the Coil → SwiftUI
`AsyncImage` mapping, already in the table) + ADR-0013 parity (trust the re-fetched `hasAfterPhotos`); (d) applies
ADR-0016 AR-PRIV-4 (purpose strings in-ticket, the API_BASE_URL/fonts `info.properties` precedent). **No new
trade-off rises to ADR-0021's bar** (a decision that could move the deployment floor, or that sets a wholly new
canonical archetype with rejected alternatives that must be defended on the record). The **`UIViewController
Representable` idiom is genuinely the repo's first** — but it is a *direct application* of ADR-0018 D2 (wrap a
platform control as a native-SwiftUI brand-skin seam), not a new principle; it is harvested into `patterns-mobile`
as a canonical idiom, the same living-doc fold-in CH-5 of ADR-0018 reserves for "a new control mapping a feature
surfaces."

**IN — T-0308 acceptance scope:**
- **Slice A:** the **`CameraOrLibraryPicker`** `UIViewControllerRepresentable` in `CleansiaCore/Components` (a
  camera-capable `UIImagePickerController` behind a SwiftUI seam — decision (a)) + a **pure `ImageCompressor`** Core
  helper (downscale longest-side → 1920px aspect-preserved + JPEG quality 0.7 → `Data` + `contentType` "image/jpeg",
  encoded OFF the main thread — decision (b)).
- **Slice B:** the **`PhotosSection`** view (two rails Before/After, the Add tile → an action sheet Take Photo /
  Choose from Library — decision (a)) + the **`OrderPhotosViewModel`** (the `OrderPhotosUiState` Loading/Error/Loaded
  + the per-rail mutation substate + the `mutationVersion`-style parent-refresh bump) over the photo methods on the
  orders repo (via the ADR-0019 spine), reading back with SwiftUI **`AsyncImage`** (decision (c)), and the
  Complete-unblock wiring trusting the **re-fetched `OrderItem.hasAfterPhotos`** (decision (c)).
- **The two plist keys** (`NSCameraUsageDescription` + `NSPhotoLibraryUsageDescription`) added IN-TICKET to the
  **partner** `project.yml` `info.properties` + localized ×5 via `InfoPlist.strings` (decision (d)).

**DEFERRED — explicitly out of T-0308, with the ticket each lands in:**
- **The customer app's camera plist keys** → **T-0314** (decision (d): the Customer app carries its own
  `NSCameraUsageDescription`/`NSPhotoLibraryUsageDescription` when T-0314's dispute-evidence capture lands — pre-adding
  them to the customer `project.yml` now ships a declared-but-unused capability, an AR-PRIV-4 "no purpose string for a
  capability the app does NOT use" risk).
- **The photo-upload ownership / EXIF-strip gate** → **SECURITY** (`security/ios-orders.md`, ruled in parallel — out
  of this record).

#### Decision (a) — THE CAPTURE SEAM: a camera-capable `UIImagePickerController` behind a NEW `CleansiaCore/Components` `UIViewControllerRepresentable` (the repo's FIRST — make it the canonical "imperative-UIKit-controller-behind-a-SwiftUI-seam" idiom); the Add tile opens an action sheet Take Photo / Choose from Library (APPLIES ADR-0018 D2/D3 — no new ADR)

**RULING: build a `UIViewControllerRepresentable` wrapping a camera-capable `UIImagePickerController` in
`CleansiaCore/Components` (sketch name `CameraOrLibraryPicker`; the exact Swift surface is the dev's, the contract is
fixed: it presents a `sourceType` picker and returns ONE picked `UIImage`/`Data` via a callback, dismissing itself).
This is the repo's FIRST `UIViewControllerRepresentable` — make it the CANONICAL "imperative-UIKit-controller-behind-
a-SwiftUI-seam" idiom and HARVEST it into `patterns-mobile.md`. The single Add tile (the Android single-affordance
rail layout) opens a NATIVE `.confirmationDialog` action sheet — `Take Photo` (`.camera`) / `Choose from Library`
(`.photoLibrary`) / `Cancel` — which then presents the representable with the chosen `sourceType`.**

- **Why a `UIImagePickerController` representable (vs the rejected alternatives):**
  - **PHPicker (library-only)** — REJECTED: `PHPickerViewController` is the modern library picker but has **no
    camera source**; the T-0308 ticket requires **camera capture**, so PHPicker fails the requirement outright.
  - **AVFoundation (custom capture pipeline)** — REJECTED: a bespoke `AVCaptureSession` camera UI is **over-engineered**
    for "take/pick one photo of a cleaning job" — it re-builds the system camera (preview layer, orientation, flash,
    permissions UX) that `UIImagePickerController` provides for free, adds large surface to test, and diverges from
    "native, no extra framework" (ADR-0018 D2). Reserved only for a future requirement the system camera can't meet
    (none here).
  - **`UIImagePickerController` representable** — CHOSEN: it is the one native control that does **both** camera and
    library from a single API (`sourceType = .camera | .photoLibrary`), maps cleanly to the Android single-Add-tile
    rail (one affordance → an action-sheet source choice), and is wrapped as a `CleansiaCore` brand-skin seam (ADR-0018
    D2 — the `Cleansia*` brand-skin-over-native posture). It is `UIImagePickerController`-deprecation-tolerant for the
    floor (it is **not** deprecated on iOS 16; it remains the canonical camera-source picker).
- **Why a `UIViewControllerRepresentable` (and why it earns its place in the catalog):** SwiftUI has **no** native
  camera/photo-source control on the iOS-16 floor — the platform answer is to wrap the UIKit controller in a
  `UIViewControllerRepresentable` (the controller analogue of the `UIViewRepresentable`/`MKMapView` map seam ADR-0014
  D6′ already established for the *view* case). This is the repo's **first** `UIViewControllerRepresentable`; homing it
  in `CleansiaCore/Components` and harvesting the idiom makes the **next** imperative-UIKit-controller need (a future
  document/share/contact picker) cheaper and consistent — it earns its place on the "makes future changes cheaper"
  bar. It does **not** introduce a new principle: it is ADR-0018 D2 applied to the *controller* case (the existing
  `UIViewRepresentable` MapKit seam is the *view* sibling), folded into the catalog per ADR-0018 CH-5 (a new control
  pattern a feature surfaces is a living-doc/catalog fold-in, not a superseding ADR).
- **THE CATALOG CORRECTION (the false precedent):** any claim that the `AddressPicker` (T-0306) established a
  `UIViewControllerRepresentable` precedent is **FALSE** — the `AddressPicker` is **pure MapKit/SwiftUI** (the iOS-16
  `Map(coordinateRegion:annotationItems:[])` SwiftUI view + a SwiftUI overlay pin + `CLGeocoder`/`MKLocalSearch`); it
  uses **neither** a `UIViewControllerRepresentable` **nor** a `UIViewRepresentable` (the `MKMapView`/
  `UIViewRepresentable` surface is `fullBleedMap` in T-0307, a *view* representable, not a *controller* one).
  `CameraOrLibraryPicker` is therefore genuinely the **first `UIViewControllerRepresentable`** in the repo. (Verified:
  §7.6/§7.9 + `patterns-mobile` describe the AddressPicker as SwiftUI `Map(coordinateRegion:)` + `CLGeocoder`; no
  representable claim exists in the catalog today, so the correction is a guard against the claim re-entering — see the
  `patterns-mobile` harvest below.)
- **The Gate-DP camera-vs-gallery divergence (architect sign-off — decision (a) half):** the Android partner
  `PhotosSection.kt` Add tile launches **`ActivityResultContracts.GetContent()` with `"image/*"`** — **GALLERY-ONLY**
  (`:146-161,200`); it has **no camera path**. The T-0308 ticket calls for **camera capture**, so iOS ships **camera +
  library** (the action sheet) — an explicit, recorded **iOS ENHANCEMENT** over Android's gallery-only. This is a
  Gate-DP **divergence with architect sign-off**: *"Android Add tile = gallery-only (`GetContent`); iOS Add tile =
  Take Photo / Choose from Library (camera + library), the ticket's camera requirement; the divergence ADDS a source
  affordance, it does NOT move layout/flow/branding (same single Add tile, same two rails, same thumbnail strip)."* It
  passes Gate-DP #3 (component-only, layout/flow/branding identical). **The Android gallery-only is itself a small gap**
  (a partner can't shoot a job photo in-app on Android) → a PM-filed Android follow-up may add a camera source there
  (the Parity rule's "Android is thin → iOS does it right, file the Android catch-up"), independent of the iOS wave.

#### Decision (b) — THE BASE64 COMPRESSION TARGET: downscale longest-side → 1920px (aspect-preserved) + JPEG quality 0.7 + contentType "image/jpeg", encoded OFF the main thread — a PURE Core helper (strict TDD); a recorded iOS pixel-dimension divergence for the perf reason (APPLIES the Parity rule + ADR-0018 D3 — no new ADR, no optimizer pass)

**RULING: the iOS upload downscales to a max longest-side dimension of 1920px (aspect-preserved, never upscale) +
JPEG quality 0.7, `contentType = "image/jpeg"`, encoded OFF the main thread (a background `Task`/queue), as a PURE
`CleansiaCore` helper (`ImageCompressor`) built strict-TDD. This is an iOS-only DIVERGENCE — Android ships RAW camera
bytes uncompressed (`PhotosSection.kt:155-159`: `readBytes()` → `Base64.encodeToString(bytes, NO_WRAP)`, comment
acknowledging "Base64 encoding can be slow for multi-MB images") so a 3–8MB image is base64-inflated ~33% over the
JSON body + held in memory. The iOS downscale+JPEG changes pixel dimensions vs Android — recorded as a deliberate
divergence with the perf rationale. It is a bounded pure helper, so the architect ruling SUFFICES — no optimizer pass.**

- **The numbers (confirm + rationale):** **1920px longest side** (aspect-preserved, never upscale a smaller image) +
  **JPEG quality 0.7** + **`contentType = "image/jpeg"`** + **`fileName`** carried through as `"<uuid>.jpg"` (the
  Android-derived-filename parity, simplified — Android derives it from the gallery URI's last path segment; an iOS
  camera capture has no URI, so a generated `.jpg` name is the clean iOS equivalent). 1920px is a sensible
  "job-evidence" resolution (full-HD long edge — legible detail, ~0.2–0.5MB JPEG vs 3–8MB raw); 0.7 is the standard
  "visually lossless enough for a photo record" quality. These are confirmed as the target; the dev tunes nothing
  outside this without a re-record.
- **Why iOS diverges from Android's raw bytes (the perf call):** the transport is **base64-over-JSON**, which inflates
  the payload **~33%** AND materializes the whole encoded string in memory (the `SaveOrderPhotosCommand` body). Android
  ships raw multi-MB bytes through that path and even comments on the cost; iOS **does it right** — a 1920/0.7 JPEG is
  ~10–30× smaller, so the base64 body + the in-memory string are bounded, the upload is faster, and the OOM risk on
  older 2017 floor devices (iPhone 8/X, the ADR-0014 reach) is removed. This is the canonical Parity-rule shape
  (*Android is wasteful → iOS does it right, record the divergence, the Android perf fix is a separate catch-up*).
- **Recorded as a Gate-DP-adjacent divergence (it changes pixels, not layout):** *"Android uploads raw camera bytes
  (no downscale); iOS downscales to 1920px longest-side + JPEG 0.7 before base64 — a deliberate iOS perf divergence
  (smaller base64-over-JSON body + bounded memory), changing the uploaded pixel dimensions, not the
  layout/flow/branding."* (Strictly this is a *behavior/perf* divergence under the Parity rule rather than a Gate-DP
  *component* swap — recorded in both registers so a reviewer doesn't flag the dimension difference as a parity bug.)
  The Android raw-bytes perf fix (add a downscale before base64) is a PM-filed Android follow-up, independent of the
  iOS wave.
- **It is a PURE helper → strict TDD, no optimizer pass (the architect's call, as briefed):** `ImageCompressor` takes
  a `UIImage` (or `Data`) → returns `(Data, contentType: String)`; it is a deterministic, side-effect-free transform
  (the off-main-thread *call site* is the VM's, the helper itself is pure). Its bounds make it ideal for red-first unit
  tests (a >1920 image scales to ≤1920 longest side aspect-preserved; a ≤1920 image is NOT upscaled; output is JPEG;
  output bytes < input for a large image). It is **bounded** (one transform, no allocation strategy / streaming /
  threading policy to optimize), so the architect ruling SUFFICES — **no optimizer pass** is warranted (an optimizer
  pass is for an unbounded hot path / allocation profile; a single bounded downscale is not one).

#### Decision (c) — READ-BACK + IMAGE LOADING: read via `orderGetPhotos → GetOrderPhotosOrderPhotoDto.blobUrl`, render with iOS-16 SwiftUI `AsyncImage` (the Coil `SubcomposeAsyncImage` → `AsyncImage` Gate-DP component swap, no 3rd-party dep); the Complete gate trusts the RE-FETCHED `OrderItem.hasAfterPhotos`, NOT `GetOrderPhotosResponse.afterPhotoCount` (APPLIES ADR-0018 D3 + ADR-0013 parity — no new ADR)

**RULING (CONFIRMED as briefed): read back via `orderGetPhotos(orderId) → GetOrderPhotosResponse.photos`, each a
`GetOrderPhotosOrderPhotoDto` carrying `blobUrl`; render each thumbnail with the iOS-16 SwiftUI `AsyncImage`
(`init(url:content:placeholder:)`, with a loading placeholder + a visible failure state). This is the ADR-0018 D3
canonical **Coil `SubcomposeAsyncImage` → SwiftUI `AsyncImage`** component swap (already in the mapping table) — a
consistent Gate-DP component swap like the prior MapKit/native-confirm swaps, with NO 3rd-party dependency (no
Kingfisher needed; `blobUrl` is a fresh SAS URL per fetch). AND: the Complete gate trusts the RE-FETCHED
`OrderItem.hasAfterPhotos` (the loaded order's server-recomputed flag) — it does NOT short-circuit off
`GetOrderPhotosResponse.afterPhotoCount`.**

- **The image-loading swap (confirm):** Android renders thumbnails with Coil `SubcomposeAsyncImage` (distinct loading
  / error states, `PhotosSection.kt:235-272`). iOS uses SwiftUI **`AsyncImage`** — the ADR-0018 D3 table's
  `Coil AsyncImage → SwiftUI AsyncImage` row (verified present in the catalog) — with `AsyncImage`'s `phase`-based
  content closure giving the same loading-spinner / broken-image-fallback the Android `SubcomposeAsyncImage` shows
  (same frame/aspect/placeholder layout — the Gate-DP "what stays identical" column). **No Kingfisher / no 3rd-party
  dep** (ADR-0018 D2 / ADR-0014 no-extra-framework): the partner `blobUrl` is a per-fetch SAS URL, so cross-fetch
  caching parity is not load-bearing here; if a *future* surface needs disk-cache parity, Kingfisher is the
  table-sanctioned fallback (a scoped dependency + a living-doc note), not now. **Recorded Gate-DP divergence:**
  *"Coil `SubcomposeAsyncImage` → SwiftUI `AsyncImage`; same thumbnail frame/aspect + loading/broken-image states;
  component-only, no layout/flow/branding change, no 3rd-party dep."*
- **The Complete gate trusts the re-fetched `hasAfterPhotos` (confirm — the parity, verified in source):** Android's
  Complete footer reads **`canComplete = order.hasAfterPhotos == true`** (`OrderDetailScreen.kt:558`) — the
  **re-fetched `OrderItem.hasAfterPhotos`** (the server-recomputed flag on the loaded order), kept live by the
  `PhotosSection` `onPhotosChanged` bump → the parent's `onContentMutated` refresh (`:133`, the `mutationVersion`
  parity). It does **NOT** count `GetOrderPhotosResponse.afterPhotoCount` to decide Complete. iOS mirrors this:
  decision (c) of §7.9 (the `OrderPrimaryAction` `.complete`/`.completeBlocked` split) consumes
  `hasAfterPhotos` derived from the **re-fetched** `OrderItem`; after a successful upload/delete, the photos VM bumps a
  parent-refresh signal (the `mutationVersion` analogue) so the OrderDetail VM re-fetches the order and
  `hasAfterPhotos` flips, unblocking Complete. **Deviation a reviewer rejects:** the Complete gate computed from
  `GetOrderPhotosResponse.afterPhotoCount` (or any client-side photo count) instead of the re-fetched
  `OrderItem.hasAfterPhotos` — that forks the gate's source-of-truth from the server flag the backend validator also
  enforces (the `AfterPhotosRequired` safety net stays authoritative; `.completeBlocked` is the client soft hint).
- **The mutation/refresh shape (parity):** the iOS `OrderPhotosViewModel` mirrors `OrderPhotosViewModel.kt` — sealed
  `OrderPhotosUiState` (Loading/Error/Loaded), a per-rail mutation substate (`isUploading` drives the Add-tile spinner;
  `deletingId` drives the specific tile's spinner), a `mutationVersion`-style monotonic bump that the parent observes
  to refresh the surrounding order (keeping `hasAfterPhotos` live). Single-photo upload via the **batch-of-one**
  `orderSavePhotos` (one `SaveOrderPhotosPhotoToSave` in the `photos` array — the `OrdersRepository.kt:264-291`
  parity); delete via `orderDeletePhoto(photoId)` then the VM invalidates its own orderId (the delete contract carries
  only `photoId`). Upload windows port verbatim: `canUploadBefore = status ∈ {_3 OnTheWay, _4 InProgress}`,
  `canUploadAfter = status == _4 InProgress` (`OrderDetailScreen.kt:530-532`); terminal orders (Completed/Cancelled)
  render read-only (no Add tile, no delete).

#### Decision (d) — PLIST SCOPE: the two `NS*UsageDescription` keys added IN-TICKET to the PARTNER `project.yml` `info.properties` (the API_BASE_URL/fonts precedent), localized ×5 via `InfoPlist.strings`; Customer carries its own at T-0314 (APPLIES ADR-0016 AR-PRIV-4 — no new ADR; the owner approves the WORDING async, non-blocking)

**RULING: `NSCameraUsageDescription` + `NSPhotoLibraryUsageDescription` are added IN-TICKET to the PARTNER app's
`CleansiaPartner/project.yml` `targets.CleansiaPartner.info.properties` (the same mechanical add as the existing
`API_BASE_URL` + `UIAppFonts` keys, `:43-51`), localized ×5 via `InfoPlist.strings` — NOT deferred to an owner
manual_step. SCOPE = PARTNER-ONLY NOW; the Customer app carries its own two keys at T-0314 (its dispute-evidence
capture). The owner approves the WORDING async (non-blocking — the keys land in-ticket; the strings can be revised
without re-touching the structure).**

- **In-ticket, not a deferred manual_step (the precedent):** the partner `project.yml` already declares
  `info.properties` keys mechanically (`API_BASE_URL`, `UIAppFonts`, `:43-51`); adding two `NS*UsageDescription` keys
  is the **same XcodeGen-`info.properties`** edit, fully in the ios dev's hands — there is no owner-only step (unlike
  the `mobile-spec-regen` or signing). ADR-0016 **AR-PRIV-4** already anticipates exactly these keys "for partner
  photos (T-0308)" and requires they ship **with the capability, in-ticket** (Gate-AR: "any new capability carries its
  purpose string + manifest entry + locale strings in-ticket, not deferred"). Deferring them would ship a camera that
  prompts with an empty/missing purpose string → an instant crash-on-first-use + an App Review rejection.
- **Localized ×5 via `InfoPlist.strings` (AR-QUAL-1 / reviewer #10):** the two purpose strings are NOT raw literals in
  `project.yml` — they are `InfoPlist.strings` keys with en/cs/sk/uk/ru values (the i18n-completeness rule). The
  strings describe the REAL use ("Take photos of the job's before/after state") — no generic/empty string (AR-PRIV-4).
- **Partner-only now; Customer at T-0314 (the scope call):** pre-adding the two keys to the Customer app's
  `project.yml` now would declare a camera/photo-library capability the **Customer app does not yet exercise** — an
  AR-PRIV-4 "**no purpose string for a capability the app does NOT use**" risk (a reviewer flags a declared-but-unused
  capability). T-0314 (customer dispute-evidence capture) adds the Customer keys **with** that capability, the same
  in-ticket discipline. **Recommendation taken: Partner-only now.**
- **The PrivacyInfo manifest (AR-PRIV-1) carries the photos data-type:** if the partner `PrivacyInfo.xcprivacy`
  doesn't already declare the **photos** collected-data type + any required-reason API the capture/encode touches, this
  ticket adds it (the manifest is the AR-PRIV-1 sibling of the purpose string — both ship with the capability). A
  declared-but-not-present, or present-but-not-declared, photos type is an AR-PRIV-1 finding.

#### The recorded Gate-DP / Parity divergences (T-0308 — all component-or-behavior, none touch layout/flow/branding)

1. **Add tile: Android gallery-only (`GetContent("image/*")`) → iOS camera + library (action sheet Take Photo /
   Choose from Library)** — decision (a). An iOS ENHANCEMENT (the ticket's camera requirement); the divergence ADDS a
   source affordance, keeping the single Add tile + two rails + thumbnail-strip layout. Architect sign-off; Android
   camera-source catch-up is a PM-filed follow-up.
2. **Upload bytes: Android raw camera bytes (no downscale) → iOS downscale 1920px longest-side + JPEG 0.7 before
   base64** — decision (b). A deliberate iOS PERF divergence (smaller base64-over-JSON body + bounded memory on the
   2017 floor); changes uploaded pixel dimensions, not layout/flow/branding. Recorded so a reviewer doesn't flag the
   dimension difference; Android perf catch-up is a PM-filed follow-up.
3. **Thumbnail loading: Coil `SubcomposeAsyncImage` → SwiftUI `AsyncImage`** — decision (c). The ADR-0018 D3 table
   mapping; same frame/aspect + loading/broken-image states; component-only, no 3rd-party dep.

#### Reviewer-check addition (T-0308+)

- **#32 (§7.10 (a)/(b)/(c)/(d)) — the partner photo flow is the canonical shape.** **(a) capture seam** — photo
  capture goes through the Core **`UIViewControllerRepresentable`** (`CameraOrLibraryPicker`) wrapping a camera-capable
  `UIImagePickerController`; the single Add tile opens a native action sheet (Take Photo / Choose from Library); the
  camera+library enhancement over Android's gallery-only is the noted Gate-DP divergence. **Findings:** a feature/VM
  hand-rolling `UIImagePickerController` (or AVFoundation) outside the Core seam; a PHPicker-only (no-camera) picker
  (fails the requirement); a representable that imports UIKit into a feature/VM rather than living in
  `CleansiaCore/Components`. **(b) compression** — the upload runs through the pure Core **`ImageCompressor`** (1920px
  longest-side aspect-preserved + JPEG 0.7 + `image/jpeg`, off the main thread); the divergence from Android's raw
  bytes is noted. **Findings:** raw/un-downscaled bytes base64'd (the Android shape copied — the un-approved perf
  divergence); main-thread base64 encode; an arbitrary dimension/quality not 1920/0.7 without a re-record. **(c)
  read-back + gate** — thumbnails render via SwiftUI **`AsyncImage`** (no 3rd-party dep); the Complete gate consumes
  the **re-fetched `OrderItem.hasAfterPhotos`** (kept live by the post-mutation parent refresh), NOT
  `GetOrderPhotosResponse.afterPhotoCount` / any client photo count. **Findings:** a 3rd-party image lib for the
  partner thumbnails; the Complete gate computed off `afterPhotoCount`; an upload/delete that doesn't bump the parent
  order refresh (so `hasAfterPhotos` goes stale). **(d) plist** — the two `NS*UsageDescription` keys are in the
  PARTNER `project.yml` `info.properties` **in-ticket**, localized ×5 via `InfoPlist.strings`, describing the real use;
  the `PrivacyInfo.xcprivacy` photos type is declared (AR-PRIV-1). **Findings:** a deferred/owner-manual plist key; a
  missing/generic/empty purpose string; a non-localized purpose string; the keys pre-added to the **Customer**
  `project.yml` before T-0314 (a declared-but-unused-capability AR-PRIV-4 risk). (Composes with the §7.9 #31 action
  table — `.complete`/`.completeBlocked` consume `hasAfterPhotos`; this check governs how `hasAfterPhotos` is produced
  + kept live.) **The photo-upload ownership / EXIF gate is SECURITY's** (`security/ios-orders.md`) — out of #32.

#### New CRC roles (added with the T-0308 wiring)

- **`ios-camera-library-picker`** (new, `CleansiaCore/Components`) — the `CameraOrLibraryPicker`
  `UIViewControllerRepresentable` wrapping `UIImagePickerController` (decision (a); the repo's FIRST
  `UIViewControllerRepresentable`): *responsibility:* present a camera- or library-source system picker and return ONE
  picked image (as `UIImage`/`Data`) via a callback, then dismiss itself. *Collaborators:* `UIImagePickerController`,
  the SwiftUI host that presents it (after the Take-Photo/Choose-from-Library action sheet). *Does NOT know:* the order
  / photo type / upload contract, base64 or compression (that's `ImageCompressor` + the VM), or who may upload
  (SECURITY). The canonical imperative-UIKit-controller-behind-a-SwiftUI-seam idiom (harvested to `patterns-mobile`).
- **`ios-image-compressor`** (new, `CleansiaCore`) — the pure `ImageCompressor` helper (decision (b)):
  *responsibility:* downscale a source image to ≤1920px longest side (aspect-preserved, never upscale) + JPEG-encode
  at quality 0.7, returning `(Data, contentType: "image/jpeg")`. *Collaborators:* none (a pure deterministic
  transform; the off-main-thread call is the VM's). *Does NOT know:* the upload contract, base64 encoding (the
  transport layer's), the order/photo type, or threading policy (the caller schedules it off-main).
- **`ios-order-photos-vm`** (new, partner orders feature) — the `OrderPhotosViewModel` (the `OrderPhotosViewModel.kt`
  parity, decision (c)): *responsibility:* load/refresh the order's photos (sealed `OrderPhotosUiState`
  Loading/Error/Loaded), upload (compress → base64 → `orderSavePhotos` batch-of-one) and delete
  (`orderDeletePhoto`) with per-rail mutation substate (`isUploading`/`deletingId`), and bump a parent-refresh signal
  (the `mutationVersion` analogue) so the OrderDetail order re-fetches and `hasAfterPhotos` stays live; surface errors
  via the `SnackbarController`. *Collaborators:* the orders repo's photo methods (via the ADR-0019 spine),
  `ios-image-compressor`, `CameraOrLibraryPicker` (via the section view), `SnackbarController`. *Does NOT know:* the
  representable's UIKit internals, who owns the order (SECURITY §"ios-orders" O1–O4 — the server is authoritative), the
  OrderDetail sheet's snap mechanics, or how the Complete footer renders the action (`ios-order-primary-action`).

#### Test contract (T-0308 — red-first)

- **TC-IOS-IMG-COMPRESS** (the pure `ImageCompressor` — strict TDD): a >1920px image scales to ≤1920px longest side
  **aspect-preserved**; a ≤1920px image is **NOT upscaled** (dimensions unchanged); the output is **JPEG**
  (`contentType == "image/jpeg"`); the output byte count is **< input** for a large image. (Pure function, no
  threading in the unit.)
- **TC-IOS-PHOTOS-GATE** (the Complete-unblock parity): after a successful After-photo upload the photos VM bumps the
  parent-refresh signal → the OrderDetail re-fetch flips `OrderItem.hasAfterPhotos` true → `OrderPrimaryAction.action`
  returns `.complete` (was `.completeBlocked`); the gate reads the **re-fetched `hasAfterPhotos`**, NOT
  `GetOrderPhotosResponse.afterPhotoCount` (a test that an `afterPhotoCount > 0` with `hasAfterPhotos == false` still
  yields `.completeBlocked` — the server flag is the source of truth).
- **TC-IOS-PHOTOS-UPLOAD** (the upload shape): `upload(type, image)` compresses (via `ImageCompressor`) → base64 →
  `orderSavePhotos` **batch-of-one** (one `SaveOrderPhotosPhotoToSave` carrying `BlobFileDto{fileName, base64Content,
  contentType: "image/jpeg"}`); the `PhotoType` maps `_1=Before`/`_2=After`; a failure snackbars + clears `isUploading`
  without bumping the parent (the `OrderPhotosViewModel.kt:104-107` parity).

### 7.11 T-0309 (Phase-5 partner EARNINGS / Invoices / PeriodPay — read-only over EmployeePayroll: getPeriodPays + getPagedInvoices + getInvoiceById + downloadInvoice→PDF) — SECURITY read-scoping gate (recorded 2026-06-27, security reviewer)

> **SECURITY sub-note (the read-side analogue of the §7.8 / T-0339 `GetPagedOrders` gate).** Full S1–S10
> walk + the binding iOS client rules (E1–E4) + the required test + the PDF-PII handling rule live in
> `agents/backlog/security/ios-earnings.md`. This is the index pointer + the headline ruling.

**security_touching: YES** (own-data financial read; the invoice PDF carries bank/payment PII —
`VariableSymbol`/`SpecificSymbol`/`PaymentReference`/`BankTransferNote`). **Verdict: PASS-the-design.**

**The headline (the T-0339 contrast):** UNLIKE `GetPagedOrders` (which trusted the client
`Filter.EmployeeId` → T-0339 filed), **all four payroll handlers ALREADY pin to the JWT caller** for
non-admins — there is **NO** T-0339-class over-read here and **NO backend read-scoping follow-up
ticket is needed.** Traced on this Mac + the existing `GetPeriodPaysOwnershipTests` ran **green (4/4)**:
- `GetPeriodPays.cs:52-61` — non-admin foreign `employeeId` → `EmployeeNotFound` before any read.
- `GetPagedInvoices.cs:33-43` — non-admin: the handler **overwrites** `employeeIdFilter` with the
  server-resolved caller id and **ignores `Filter.EmployeeId`** (empty page if unresolvable). This is
  exactly the pin T-0339 had to ADD to `GetPagedOrders` — payroll already has it.
- `GetInvoiceById.cs:57-66` / `DownloadInvoice.cs:49-58` — non-admin `invoice.EmployeeId != caller`
  → `InvoiceNotFound` (NotFound, not Forbidden); the blob is never fetched/streamed for a foreign id.

**S4 (DTO leak) — PASS.** DTOs carry only the caller's OWN fields; `EmployeeInvoice` (`ITenantEntity`)
has **no IBAN / raw bank-account number**; no `TenantId`/`UserId`/Stripe id/hash; no other-employee
PII. **S5 (rate limit) — LATENT GAP, folded into BSP-4d:** `EmployeePayrollController`'s 4 routes carry
**no** `[EnableRateLimiting]` while every sibling on the host does, and the global limiter is a
no-limiter for authenticated callers — so the payroll reads are unthrottled (read-only enumeration
surface; LOW/latent; **iOS cannot fix — backend hardening**, not a T-0309 blocker).

**Binding iOS client rules (E1–E4):** E1 own-server-derived id only (`currentEmployeeId()`, the §7.7
O3 precedent — never screen input); E2 no foreign-id echo; E3 download own invoices only (`invoiceId`
from the caller's OWN list); E4 the downloaded PDF is **deleted from cache/temp after the preview is
dismissed** (no PII-bearing PDF left resident in `Caches/`). **PrivacyInfo:** **NO** new
`NSPrivacyCollectedDataType` entry for the download (a round-trip of the caller's OWN record is not
App-Store "collection"; contrast T-0308's camera/library *capability* purpose strings) — it inherits
the standing AR-PRIV-1 required-reason-API audit only.

**Required test:** **TC-IOS-EARNINGS-OWNERSHIP** — the facade/VM building the requests asserts the
outgoing id always equals the caller's own `currentEmployeeId()` even if a foreign id is injected into
screen state, and `downloadInvoice` runs only with an `invoiceId` from the caller's own fetched list;
PDF-cleanup (E4) covered by a remove-on-dismiss unit test (or folded into the architect's QuickLook
coordinator test). Backend ownership already proven by `GetPeriodPaysOwnershipTests` (green here).

**The architect rules nav / PDF preview mechanism / number-format in parallel — SECURITY stays out of
those; it owns only the read-scoping + the PDF-PII-cleanup-is-mandatory rule.** Cross-ref:
`agents/backlog/security/ios-earnings.md`.

---

### 7.12 T-0309 (Phase-5 partner earnings + invoices + PeriodPay) — acceptance scope + the four Understand-pass rulings (recorded 2026-06-27, architect)

> **The read-scoping / PII gate (who may read which employee's earnings/invoices/PeriodPay, the own-id-only rule, the
> mandatory post-preview PDF cache-cleanup) is ruled by SECURITY in parallel in §7.11 (`security/ios-earnings.md`,
> E1–E4 + TC-IOS-EARNINGS-OWNERSHIP) — this architect record stays OUT of it.** The decisions below rule only the
> *nav shape*, the *PDF-viewing seam*, the *money/date formatting*, and the *stats source* — never who may read whose
> payroll. SECURITY's E4 (delete the previewed PDF from cache on dismiss) composes with this record's `QuickLookPreview`
> coordinator (decision (b)); SECURITY's E1 (own-server-derived id only) composes with the no-`UserProfileStore` fact
> (§7.5 D2) — iOS sources the caller-own id the iOS way, which SECURITY rules. Both records cover the same ticket from
> their two mandates; neither contradicts the other.

T-0309 (`M`, `phase/ios-phase5`, depends_on T-0304✓) builds the partner **earnings / invoices / PeriodPay** surface
over the generated `PartnerEmployeePayrollAPI` — all four operations ride the **ADR-0019 spine** (the Core-spine-backed
`RequestBuilderFactory`; no auth code): `employeePayrollGetPeriodPays` / `employeePayrollGetPagedInvoices` /
`employeePayrollGetInvoiceById` / `employeePayrollDownloadInvoice`. **The spec is already regen'd** (the regen'd
`partner-mobile-api.json` carries `EmployeePayroll_{GetPagedInvoices,GetInvoiceById,GetPeriodPays,DownloadInvoice}` —
verified `:2977/:3162/:3262/:3368`), so **there is NO owner codegen step** for T-0309. It **replaces** the partner
shell's 3rd tab placeholder (`PartnerShellView` `.invoices` = `PlaceholderTab(ticket:"T-0309")`) and wires the
Dashboard's currently-inert `onOpenEarnings`. **2 slices (§6):** A = Earnings summary + PeriodPay; B = invoices list +
detail + PDF.

**This is a "record, not ADR" ruling — the §7.2/§7.4/§7.5/§7.6/§7.7/§7.9/§7.10 precedent.** All four decisions
**APPLY accepted ADRs + prior records**: (a) applies **ADR-0020** (the intra-audience push is a `NavigationStack`
WITHIN an audience; the audience tab is the surface) + the §7.7 D1 in-tab-`NavigationStack` precedent + ADR-0018
Gate-DP (the recorded nav divergence form); (b) applies the **T-0308 §7.10 D1 Core-seam precedent** (a UIKit
controller behind a `CleansiaCore/Components` seam, harvested) + ADR-0018 D2 (brand-skin-over-native) — `QuickLookPreview`
is the *next* application of that idiom, not a new principle; (c) applies the §7.5 D4 / §7.7 D4 Core-utility-helper
precedent (the `EmailValidator`/`PasswordPolicy`/`AppSettingsStore` "lift the duplicated thing to a small typed Core
helper" pattern) + the `patterns-mobile` harvest-on-3+-call-sites rule; (d) applies **ADR-0013** parity + Core/DRY
(reuse the existing `PartnerDashboardClient.getStats`, the Android `EarningsSummaryViewModel` parity exactly).
**No new trade-off rises to an ADR's bar** (a decision that could move the deployment floor, or that sets a wholly new
canonical archetype with rejected alternatives that must be defended on the immutable record). The PDF-viewing call is
a **seam choice among three options** — but it is *the same shape* as the §7.10 D1 capture-seam decision (a system
UIKit controller wrapped as a Core `CleansiaCore/Components` representable), so it is a §7.12 line + a `patterns-mobile`
harvest, not an ADR.

**Android parity source (the iOS port mirrors it):** `partner-app/.../features/earnings/` (`EarningsSummaryScreen.kt`/
`EarningsSummaryViewModel.kt`), `features/invoices/` (`InvoicesListScreen.kt`/`InvoicesListViewModel.kt`,
`InvoiceDetailScreen.kt`/`InvoiceDetailViewModel.kt`, `InvoiceStatusBadge.kt`), `features/payroll/`
(`PeriodPayScreen.kt`/`PeriodPayViewModel.kt`) + `data/invoices/InvoicesRepository.kt` + `data/payroll/{PeriodPayApi,
PeriodPayRepository}.kt`; nav `navigation/PartnerNavHost.kt:160-289` (the Earnings **push** + Invoices **tab** +
InvoiceDetail/PeriodPay pushes). **Gate-DP applies** (T-0309 is a screen ticket): the screens cite their Compose
counterparts; native SwiftUI; iOS-wins-on-conflict + the noted divergences (the push+tab → single-tab+stack nav swap;
the Android FileProvider/`ACTION_VIEW` → iOS QuickLook PDF swap; the Coil-family card visual language → native).

**IN — T-0309 acceptance scope:**
- **Slice A:** the **Earnings summary** screen (the `EarningsSummaryScreen.kt` parity — headline current-period card +
  Today/Week/Last-month breakdown + pay-period progress + the "View all invoices" entry row) driven by
  `PartnerDashboardClient.getStats` (decision (d)) over a sealed `UiState<DashboardStatsDto>`; the **PeriodPay** detail
  (`PeriodPayScreen.kt` parity — the per-order pay rollup for one pay period) over the generated
  `employeePayrollGetPeriodPays`, a sealed `UiState<PeriodPaySummary>`. **The `.invoices` tab roots an in-tab
  `NavigationStack` over a typed `EarningsRoute` enum** (decision (a)), **landing on the Earnings summary**.
- **Slice B:** the **invoices list** (`InvoicesListScreen.kt` parity — lifetime-total hero + per-invoice cards) over
  `employeePayrollGetPagedInvoices`, a sealed per-list `UiState<[EmployeeInvoiceDto]>` + a `RefreshPhase` enum (the
  §7.9 (e) list-state convention — **NOT** the Android E1 flag-bag, see the divergence below) **+** the PORTED
  my-invoices staleness watermark (the `InvoicesRepository.getMyInvoicesStaleness()`/`invalidateMyInvoices()` parity —
  the silent-stale resume the tab needs); the **invoice detail** (`InvoiceDetailScreen.kt` parity — hero/breakdown/
  period/references/notes cards + the "Open PDF" affordance) over `employeePayrollGetInvoiceById`; **PDF viewing** via
  the new Core **`QuickLookPreview`** seam (decision (b)) over the generated `employeePayrollDownloadInvoice`'s local
  file URL. The detail's "View period pay" row pushes `.periodPay(payPeriodId, currencyCode)` (decision (a)).

**DEFERRED / NOT replicated — explicitly out of T-0309, with the ticket each lands in:**
- **The Android E1 invoices flag-bag is NOT replicated** → **T-0337** (the standing Android-partner-E1 home). The
  Android `InvoicesListUiState` is the same `isUserRefreshing`/`isBackgroundRefreshing`/`hasLoadedOnce`/`invoices`
  flag-bag (`InvoicesListScreen.kt:125,132-148,150` reads them) the §7.9 (e) / §7.7 D5 ruling diverges from; iOS is
  born sealed-state. (Recorded Gate-DP/Parity divergence — see below.)
- **The read-scoping / PII gate** → **SECURITY §7.11** (`security/ios-earnings.md`, ruled in parallel — out of this
  record).
- **No `FileDownload` Core seam is built** (decision (b)): the generated client already downloads the PDF (writes the
  body to disk, returns a local file URL); the VM only **surfaces** that URL via a one-shot event — there is nothing to
  download-orchestrate, so a download seam would be dead abstraction.

#### Decision (a) — NAV SHAPE: the `.invoices` TAB *is* the surface — it roots an in-tab `NavigationStack` over a typed `EarningsRoute` enum landing on the **Earnings summary**; the Dashboard's `onOpenEarnings` sets `ShellModel.selection = .invoices` (mirrors `selectOrders()`), NOT a push (APPLIES ADR-0020 + §7.7 D1 + ADR-0018 Gate-DP — no new ADR)

**RULING: the partner shell's 3rd tab (`.invoices`, the slot T-0304 committed to) IS the earnings/invoices surface. It
roots an in-tab `NavigationStack` over a typed `enum EarningsRoute { case summary; case invoices; case invoiceDetail(id:
String); case periodPay(payPeriodId: String, currencyCode: String?) }` — the ADR-0020 D4 / §7.7 D1 intra-audience push
(the root `PartnerRootView` enum stays the audience selector; the tab-local `NavigationStack` is the push container
WITHIN the `.dashboard` shell, exactly as the Profile tab does). The Dashboard's `onOpenEarnings` sets
`ShellModel.selection = .invoices` (the `selectOrders()` parity the dashboard already uses for `onOpenOrders` →
T-0304's `onOpenOrders` → Orders tab) — a TAB SWITCH, **not** a push. THE TAB LANDING IS THE EARNINGS SUMMARY (the
recommended option) — Android's Earnings screen exists specifically to avoid landing on an empty invoices list for a
cleaner with no closed pay period yet. This canonicalizes the Android push+tab two-surface shape onto iOS's single-tab
+ in-tab-stack — the same Gate-DP class as the T-0304 TabView swap. Record, no new ADR.**

- **The Android shape (verified — push + tab, two entry surfaces).** Android has **two** payroll surfaces:
  `NavRoute.Earnings` is a **PUSHED** destination (`PartnerNavHost.kt:168` `onOpenEarnings = navigate(NavRoute.Earnings)`,
  rendered `:260-278`), AND `NavRoute.Invoices` is a **bottom-nav TAB** inside `MainScaffold` (`:279-289` — "the
  main-graph deep-link target… the Invoices bottom-nav tab"). The Earnings screen's "View all invoices" does **not**
  push the list — it sets `MainTab.Invoices.ordinal` and pops Earnings so `MainScaffold` animates the pager to the
  Invoices tab (`:264-275`). `InvoiceDetail` (`:247-255`) and `PeriodPay` (`:256-258`) are pushed; PeriodPay receives
  `(payPeriodId, currencyCode)` (`:251`).
- **Why iOS collapses push+tab into one tab + an in-tab stack (the ruling).** iOS already **committed** (T-0304) to the
  `.invoices` shell tab — the shell is the native `TabView` (Dashboard·Orders·Invoices·Profile, the §7.4 Slice-B shape).
  The Android "Earnings (pushed) + Invoices (tab)" split is a Compose-NavHost artifact (a pushed screen that hops to a
  tab via an ordinal key + pop). On iOS the **native** shape is: the tab owns a `NavigationStack`, the summary is the
  stack root, and the list is **one push** off the summary (the §7.7 D1 Profile-tab idiom — the tab hosts an in-tab
  `NavigationStack` over a typed route enum). This is the **same Gate-DP component class as the T-0304 TabView swap**
  (Android `MainScaffold` floating-island pill → SwiftUI `TabView`): the navigation *structure* (Earnings → Invoices →
  Detail → PeriodPay, same back-stack order) and content are identical; only the *mechanism* (a NavHost push-to-tab vs a
  tab-local stack) changes. **Layout/flow/branding are unchanged** — Gate-DP #3 passes.
- **`onOpenEarnings` is a tab SWITCH, not a push (the load-bearing parity).** The Dashboard's earnings card must land on
  the `.invoices` tab's summary root, not push a screen onto the Dashboard tab's stack — exactly as T-0304 already wired
  `onOpenOrders` to switch to the Orders tab (the `selectOrders()` shape). So `onOpenEarnings` sets
  `ShellModel.selection = .invoices`. (If the tab's stack is non-empty from a prior visit, switching to it shows
  whatever was on top; a `.popToRoot` on tab-reselect is a reasonable native polish but not required for parity — record
  it as the dev's call, defaulting to the native "remember the tab's stack" behavior, which matches Android's tab state
  retention.)
- **The tab landing = the Earnings SUMMARY (recommended), not the invoices list (the discriminator + why).** Android
  built `EarningsSummaryScreen` **specifically** to avoid the empty-list landing: its own docstring —
  *"the old flow jumped straight to InvoicesListScreen, which is empty for any cleaner whose first pay period hasn't
  closed yet — confusing and unhelpful. This screen always has meaningful content (today/week/month earnings, jobs done,
  pay-period progress, next payout) and offers 'View all invoices' as a deliberate drill-down"*
  (`EarningsSummaryScreen.kt:56-66`). So the tab root is `.summary`; `.invoices` (the list) is a push off it (the
  "View all invoices" entry row, `EarningsSummaryScreen.kt:127,332-368`). **Rejected: land the tab on the invoices
  list** — it reproduces the exact empty-landing UX Android deliberately moved away from, and it drops the Earnings
  summary (the today/week/month + pay-period card a cleaner with no invoices yet still sees). Landing on the summary is
  the Android-parity-and-better-UX choice.
- **Recorded Gate-DP divergence (architect sign-off):** *"Android = Earnings (pushed screen) + Invoices (bottom-nav tab),
  with Earnings→Invoices a pop+tab-ordinal hop; iOS = the single `.invoices` shell tab rooting an in-tab `NavigationStack`
  over a typed `EarningsRoute` enum (summary root → invoices list → detail → periodPay), `onOpenEarnings` a tab switch;
  same nav structure/content/back-stack order, the mechanism is native — the same class as the T-0304 `MainScaffold`→
  `TabView` swap; touches the component/mechanism, not layout/flow/branding."*

#### Decision (b) — INVOICE PDF VIEWING: present the generated `downloadInvoice` local file URL via a NEW Core `CleansiaCore/Components` `QuickLookPreview` (`QLPreviewController` `UIViewControllerRepresentable`), guarded on the DTO's `pdfGenerationFailed`; NO `FileDownload` seam (the generated client already downloads → the VM surfaces the URL via a one-shot event) (APPLIES the §7.10 D1 Core-seam precedent + ADR-0018 D2 — no new ADR; HARVEST to `patterns-mobile`)

**RULING: present the invoice PDF via QuickLook (`QLPreviewController`) wrapped as a NEW Core seam **`QuickLookPreview`**
— a `UIViewControllerRepresentable` in `CleansiaCore/Components` (the second member of the `CameraOrLibraryPicker`
family established by §7.10 D1: a system UIKit controller behind a `CleansiaCore/Components` SwiftUI brand-skin seam).
The generated `employeePayrollDownloadInvoice` (a `format: binary` response, verified `:3388-3398`) is mapped by the
swift5+urlsession generator to a **local file URL already written to the caches dir** (the URLSession layer streams the
body to disk) — so the VM holds that URL and surfaces it via a ONE-SHOT effect (a `PassthroughSubject`/async-stream
event, the §7.10 D3 `mutationVersion`-style one-shot, NOT a navigation route) that the screen consumes by presenting
`QuickLookPreview` over the URL. **NO `FileDownload` Core seam is built** — the generated client IS the download; a
download-orchestration seam would be dead abstraction. The "Open PDF" affordance is **guarded on the DTO's
`pdfGenerationFailed`** (the boolean on `EmployeeInvoiceDto`/`EmployeeInvoiceDetailDto`, verified spec `:6448/:6557`):
when `true`, the affordance is disabled/hidden (no point downloading a PDF that failed server-side generation).
HARVEST `QuickLookPreview` into `patterns-mobile` as the canonical "preview a downloaded document" seam. Record, no
new ADR (it is the §7.10 D1 idiom's next application). **The mandatory post-preview cache cleanup of the PII-bearing PDF
is SECURITY's E4 (§7.11) — the `QuickLookPreview` coordinator hosts that cleanup; this record fixes the seam shape, not
the cleanup mandate.**

- **Why QuickLook wrapped as a Core seam (vs the rejected alternatives):**
  - **A partner-local representable** — REJECTED: the **customer app (T-0314) reuses this** (its disputes/invoices
    surface previews PDFs/evidence), so a partner-local `QLPreviewController` wrapper would be duplicated into the
    customer target — the exact "put shared code in `:core`/`Core`, never duplicate across the two apps"
    `patterns-mobile` rule. It homes in `CleansiaCore/Components` beside `CameraOrLibraryPicker` (the §7.10 D1 precedent:
    the imperative-UIKit-controller-behind-a-SwiftUI-seam idiom lives in Core).
  - **A share-sheet (`UIActivityViewController`) / `SafariView`** — REJECTED: a share sheet is a *share/export*
    affordance, not an in-app *viewer* (it forces the user out to another app to read their own invoice — a worse
    parity than Android's in-app `ACTION_VIEW` system PDF viewer); `SafariView`/`SFSafariViewController` is for **web
    URLs**, not a local file URL (it cannot reliably render a `file://` PDF). QuickLook is the iOS-native **in-app
    document preview** — the right native analogue of Android handing the FileProvider URI to the system PDF viewer.
  - **`QuickLookPreview` (`QLPreviewController` representable)** — CHOSEN: it is the one native control that previews a
    local document in-app (PDF/images/docs), maps cleanly to Android's "system PDF viewer over the downloaded file"
    (`InvoiceDetailViewModel.kt:81-108` streams to cache → `InvoiceDetailScreen.kt:94-104` hands the FileProvider URI to
    `Intent.ACTION_VIEW`), and is the second instance of the §7.10 D1 Core-seam idiom — so harvesting it makes the next
    document-preview need (customer T-0314) free.
- **The Android → iOS PDF swap (the recorded Gate-DP divergence).** Android: the VM **streams the `ResponseBody` to the
  cache dir itself** then builds a `FileProvider` URI for `Intent.ACTION_VIEW` (`InvoiceDetailViewModel.kt:81-108`;
  `InvoiceDetailScreen.kt:91-104`), with a `notifyNoPdfViewer()` fallback if no PDF app is installed. iOS: the generated
  swift5 client **already wrote the body to disk** (the URLSession binary-response handler), so the VM holds the local
  file URL directly — **no stream-to-cache step** — and presents `QuickLookPreview` (always available, no "no viewer
  installed" branch needed — QuickLook is a system framework). **Recorded divergence:** *"Android = VM streams
  `ResponseBody` → cache → `FileProvider` URI → `Intent.ACTION_VIEW` (system PDF viewer, with a no-viewer fallback);
  iOS = the generated client writes the body to disk → the VM surfaces the local file URL via a one-shot event → the
  screen presents the Core `QuickLookPreview` (`QLPreviewController`); same in-app PDF viewing, the mechanism is native;
  no stream-to-cache step (the codegen does it), no FileProvider, no no-viewer fallback; touches the component/mechanism,
  not layout/flow/branding."*
- **The `pdfGenerationFailed` guard (the affordance gate).** The invoice DTOs carry `pdfGenerationFailed: boolean`
  (+ `pdfGenerationError`, verified spec `:6448-6454/:6557`). The "Open PDF" affordance is **disabled (or hidden)** when
  `pdfGenerationFailed == true` — downloading a server-side-failed PDF returns nothing useful. (Android renders the
  Open-PDF button unconditionally in `InvoiceDetailScreen.kt:170-176` and relies on the download failing into a
  snackbar — iOS does it *better* by gating the affordance off the flag the server already exposes; a small
  Parity-rule "iOS does it right" improvement, not a flow change. The download-failure snackbar path stays as the
  belt-and-braces fallback for a non-`pdfGenerationFailed` download error.)
- **No `FileDownload` seam (the scope guard).** The generated `employeePayrollDownloadInvoice` IS the download (binary
  response → local file URL). The VM's only job is to call it and surface the URL via a one-shot event; there is no
  retry/resume/progress/storage-permission orchestration to abstract (unlike Android's manual cache-stream, which the
  codegen subsumes). A `FileDownload` Core protocol here would be an empty seam — **not built**. (If a *future* surface
  needs true download orchestration — large files, progress, resume — that earns its own seam then, on evidence.)

#### Decision (c) — MONEY / FORMAT: a small Core `EarningsFormat` (decimal money `%,.2f` + a whole-currency `%,.0f` headline variant + ISO→local date helpers) reusing the shared currency-symbol resolution; do NOT overload `DashboardFormat.money` (which is `%.0f`); HARVEST the currency-symbol resolution to Core if it is now in 3+ places (APPLIES the §7.5 D4 / §7.7 D4 Core-utility-helper precedent + the Parity rule — no new ADR)

**RULING: introduce a small Core **`EarningsFormat`** helper (in `CleansiaCore`, the `EmailValidator`/`PasswordPolicy`
factoring) carrying: (1) **decimal money** `formatMoney(_:symbol:)` → `%,.2f` for PeriodPay rows + invoices (the
`InvoiceDetailScreen.kt:624-627`/`InvoicesListScreen.kt:477-480`/PeriodPay `%,.2f` parity); (2) a **whole-currency
headline** variant `formatMoneyWhole(_:symbol:)` → `%,.0f` for the earnings headline + breakdown numbers (the
`EarningsSummaryScreen.kt:420-423` parity); (3) **ISO→local date** helpers (`d MMM` / `d MMM yyyy`, the
`formatDate`/`parseIsoDate`/`formatShort` parity duplicated across all four Android screens). It reuses the **shared
currency-symbol resolution** (the `Currency.getInstance(code).getSymbol(Locale)` → `code` fallback, duplicated VERBATIM
in `EarningsSummaryScreen.kt:413-418` + `InvoiceDetailScreen.kt:617-622` + `InvoicesListScreen.kt:470-475`). **Do NOT
overload `DashboardFormat.money`** — it is `%.0f` (whole-currency, no thousands grouping) and is the dashboard hero's
contract; the earnings surface needs BOTH `%,.2f` (decimal, grouped) and `%,.0f` (grouped) — overloading the dashboard
helper would either break the dashboard or fork its meaning. Record, no new ADR.**

- **The two precisions are real and load-bearing (verified — the reason NOT to overload one helper).** The Android
  earnings surface deliberately uses **two** money precisions: `%,.0f` for the **earnings headline + breakdown**
  (`EarningsSummaryScreen.kt:421` — "$1,234 Kč", whole currency, the hero framing) and `%,.2f` for **invoices +
  PeriodPay** (`InvoiceDetailScreen.kt:625` / `InvoicesListScreen.kt:478` / PeriodPay — "$1,234.56 Kč", exact money on
  a billing document). Both **group thousands** (`%,`). `DashboardFormat.money` is `%.0f` (whole, **un**grouped) and is
  the dashboard hero's own contract — it is neither of the two earnings formats. `EarningsFormat` carries both grouped
  variants; the dashboard helper is left alone.
- **Why a small Core helper (the "earns its place" bar).** The currency-symbol resolution + the date parse/format are
  **duplicated verbatim across three (symbol: Earnings + InvoiceDetail + InvoicesList) and four (date) Android screens**
  — copy-paste private functions per screen. On iOS, lifting them into one tested `EarningsFormat` (the §7.5 D4
  `PasswordPolicy` / §7.7 D4 `AppSettingsStore` "the one way, in Core" factoring) makes the four screens consistent and
  the *future* format change (a 5th surface, a currency-display tweak) cheaper — it earns its place. **Rejected: per-screen
  private `formatMoney`/`currencySymbol`/`formatDate` copies** (the Android copy-paste shape — four drifting copies).
- **HARVEST the currency-symbol resolution to Core (the 3+-call-sites rule).** The `currencySymbol(code)` resolution is
  now used in **≥3 places** on iOS (Earnings, InvoiceDetail, InvoicesList — and PeriodPay threads a `currencyCode`).
  Per the `patterns-mobile` "harvest a pattern used in 3+ places" rule, it is hoisted into Core (as part of
  `EarningsFormat`, or a tiny `CurrencyFormatting` Core utility `EarningsFormat` uses). The iOS resolution is a
  `NumberFormatter(.currency)` / `Locale` symbol lookup with the **ISO-code fallback** when unknown (the
  `runCatching{...}.getOrNull() ?: code` parity — never crash, fall back to the raw code). **Client-side display only**
  — the server amounts/currency are authoritative.
- **The PeriodPay currency comes from the nav route, not the DTO (verified parity, threads decision (a)).** The
  `PeriodPaySummary` DTO **carries no currency** — Android threads `currencyCode` through the nav route from the
  launching invoice (`PeriodPayViewModel.kt:43-44` "the summary DTO carries no currency"; `:251` passes it). The iOS
  `EarningsRoute.periodPay(payPeriodId:currencyCode:)` carries it (decision (a)); `EarningsFormat` formats the PeriodPay
  rows with that threaded code's symbol. A PeriodPay reached with a nil `currencyCode` degrades to the
  symbol-less/raw-code format (the empty-symbol branch — `formatMoney` returns the bare number).

#### Decision (d) — STATS SOURCE for the Earnings summary: REUSE `PartnerDashboardClient.getStats` (the same `DashboardStatsDto` the Dashboard hero renders); do NOT duplicate onto the payroll client / call `GetPeriodPays` for the summary (APPLIES ADR-0013 parity + Core/DRY — no new ADR)

**RULING (CONFIRMED as recommended): the Earnings summary screen REUSES `PartnerDashboardClient.getStats` — the same
`dashboardGetStats` call returning the same `DashboardStatsDto` the T-0303 Dashboard hero already renders — NOT a
duplicate stats fetch on the payroll client, and NOT `employeePayrollGetPeriodPays` (which is the per-period rollup, a
different shape for a different screen). This mirrors the Android `EarningsSummaryViewModel` EXACTLY, which injects
`DashboardRepository` and calls `dashboardRepository.getStats(employeeId = null)` (`EarningsSummaryViewModel.kt:9,31-32,49`).
The Earnings summary VM is a thin sealed `UiState<DashboardStatsDto>` (Loading/Error/Loaded) over that one call.
Record, no new ADR.**

- **The Android parity is exact (verified).** `EarningsSummaryViewModel` reuses `DashboardRepository.getStats` — its own
  docstring: *"Re-uses [DashboardRepository.getStats] — same data the dashboard hero cards already render, just on its
  own dedicated surface"* (`EarningsSummaryViewModel.kt:23-29`). It does **not** call `GetPeriodPays`. The summary
  screen renders `DashboardStatsDto` fields (`currentPeriodEarnings`, `today/week/lastMonth` earnings + counts,
  `currentPayPeriodStart/End`, `nextPayoutDate`, `latestInvoiceStatus` — `EarningsSummaryScreen.kt:137-325`).
- **Why reuse (the call).** `PartnerDashboardClient.getStats` already exists and is proven (T-0303, through the ADR-0019
  spine). Duplicating the stats fetch onto the payroll client (or computing the summary from `GetPeriodPays`) would fork
  the source-of-truth for the same numbers the dashboard hero shows — a Core/DRY + parity violation. The Earnings
  summary and the Dashboard hero render the *same stats* by design; one client, one DTO. **Rejected: a payroll-client
  stats duplicate / a `GetPeriodPays`-derived summary** — two sources for one set of numbers, drift, and extra surface.
- **The `employeeId = null` parity (the own-stats read).** Android calls `getStats(employeeId = null)` — the server
  scopes to the caller's own employee from the session (the partner-host `[Permission]`-guarded, token-scoped read the
  T-0303 dashboard proved). iOS passes the same (no client employeeId). **How the caller-own scope is trusted on the
  wire is SECURITY's §7.11 read-scoping ruling** (E1, composing with the no-`UserProfileStore` fact, §7.5 D2 — iOS does
  NOT resolve the caller's `employeeId` from a client store; the server derives it). This decision rules only that the
  summary's stats SOURCE is the reused dashboard client.

#### The recorded Gate-DP / Parity divergences (T-0309 — all component/mechanism or "iOS does it right", none touch layout/flow/branding)

1. **Nav: Android Earnings (pushed) + Invoices (bottom-nav tab) with a pop+tab-ordinal hop → iOS single `.invoices`
   shell tab rooting an in-tab `NavigationStack` over a typed `EarningsRoute` enum; `onOpenEarnings` = a tab switch
   (`ShellModel.selection = .invoices`)** — decision (a). Same nav structure/content/back-stack order; the mechanism is
   native (the same class as the T-0304 `MainScaffold`→`TabView` swap).
2. **PDF: Android VM streams `ResponseBody` → cache → `FileProvider` URI → `Intent.ACTION_VIEW` (with a no-viewer
   fallback) → iOS the generated client writes the body to disk → the VM surfaces the local file URL → the Core
   `QuickLookPreview` (`QLPreviewController`)** — decision (b). Same in-app PDF viewing; the mechanism is native; no
   stream-to-cache (codegen does it), no FileProvider, no no-viewer branch.
3. **List state: the Android `InvoicesListUiState` E1 flag-bag → iOS a sealed per-list `UiState<[EmployeeInvoiceDto]>` +
   a `RefreshPhase` enum (the §7.9 (e) convention)** — NOT replicated; the per-list staleness watermark is PORTED. The
   Android E1 fix is filed at **T-0337** (the standing partner-E1 home).
4. **Open-PDF gate: Android renders the Open-PDF button unconditionally and relies on the download failing into a
   snackbar → iOS gates the affordance off the DTO's `pdfGenerationFailed` flag** — decision (b). A Parity-rule "iOS does
   it right" improvement (the server already exposes the flag); the download-error snackbar stays as the fallback for a
   non-flag failure. (The Android catch-up — gate the button off `pdfGenerationFailed` — is a PM-filed follow-up,
   independent of the iOS wave.)
5. **PeriodPay endpoint: Android hand-wrote a `PeriodPayApi` Retrofit interface (because the checked-in spec didn't
   carry `GetPeriodPays` at the time — `PeriodPayApi.kt:8-18`) → iOS uses the GENERATED `employeePayrollGetPeriodPays`
   (the regen'd spec now carries it, verified `:3262`)** — a mechanism divergence with zero behavior change; iOS rides
   the ADR-0019 spine like every other generated call, no hand-written Retrofit/verb. (The Android catch-up — drop the
   hand-written `PeriodPayApi` for the generated one once its spec is refreshed — is a PM-filed follow-up.)

#### Reviewer-check addition (T-0309+)

- **#33 (§7.12 (a)/(b)/(c)/(d)) — the partner earnings/invoices/PeriodPay surface is the canonical shape.**
  **(a) nav** — the `.invoices` shell tab roots an in-tab `NavigationStack` over a typed `EarningsRoute` enum
  (`.summary`/`.invoices`/`.invoiceDetail(id)`/`.periodPay(payPeriodId,currencyCode)`), landing on `.summary`;
  `onOpenEarnings` sets `ShellModel.selection = .invoices` (a tab switch, the `selectOrders()`/`onOpenOrders` parity),
  NOT a push; `.periodPay` carries `currencyCode` (the DTO has none). **Findings:** the earnings surface modeled as a
  pushed screen off the Dashboard tab (an ADR-0020 audience-vs-intra-audience confusion); the tab landing on the
  invoices list (the empty-landing UX Android removed); a top-level audience hop (the audience stays the `PartnerRootView`
  enum — #23). **(b) PDF** — viewing goes through the Core **`QuickLookPreview`** (`QLPreviewController`
  `UIViewControllerRepresentable` in `CleansiaCore/Components`) over the generated `downloadInvoice` local file URL,
  surfaced by a one-shot VM event; the Open-PDF affordance is gated on `pdfGenerationFailed`. **Findings:** a
  partner-local `QLPreviewController` wrapper (it must be the Core seam — the customer app T-0314 reuses it); a
  share-sheet/`SafariView` substituted for the in-app viewer; a built `FileDownload` seam (the generated client IS the
  download); an Open-PDF affordance not gated on `pdfGenerationFailed`; a VM re-streaming the body to disk (the codegen
  already did). **(c) format** — money/date go through the Core **`EarningsFormat`** (decimal `%,.2f` for invoices/
  PeriodPay + whole `%,.0f` for the earnings headline + ISO→local dates); the currency-symbol resolution is the Core
  utility (3+ call sites harvested); `DashboardFormat.money` (`%.0f`) is NOT overloaded. **Findings:** per-screen
  private `formatMoney`/`currencySymbol`/`formatDate` copies; the dashboard helper overloaded for the earnings precisions;
  a single `%,.2f`/`%,.0f` collapse losing the headline-vs-document distinction. **(d) stats** — the Earnings summary
  reuses `PartnerDashboardClient.getStats` (the `DashboardStatsDto` the Dashboard hero renders), NOT a payroll-client
  duplicate or a `GetPeriodPays`-derived summary. **Findings:** a second stats fetch on the payroll client; the summary
  computed from `GetPeriodPays`. (Composes with #13-gen — every payroll call rides the ADR-0019 spine.) **The
  read-scoping / PII gate (incl. the post-preview PDF cache-cleanup) is SECURITY's §7.11** — out of #33.

#### New CRC roles (added with the T-0309 wiring)

- **`ios-quicklook-preview`** (new, `CleansiaCore/Components`) — the `QuickLookPreview` `UIViewControllerRepresentable`
  wrapping `QLPreviewController` (decision (b); the second member of the §7.10 D1 UIKit-controller-behind-a-SwiftUI-seam
  family): *responsibility:* present a system in-app preview of ONE local document (a `file://` URL) and dismiss itself
  (its coordinator hosts SECURITY's E4 post-dismiss cache cleanup). *Collaborators:* `QLPreviewController`, the SwiftUI
  host that presents it (on the VM's one-shot URL event). *Does NOT know:* the invoice/payroll contract, how the file
  was downloaded (the generated client), who may read it (SECURITY), or anything about the document's content. The
  canonical "preview a downloaded document" seam (harvested to `patterns-mobile`), reused by the customer app (T-0314).
- **`ios-earnings-format`** (new, `CleansiaCore`) — the `EarningsFormat` pure helper (decision (c)): *responsibility:*
  format money (decimal `%,.2f` + whole `%,.0f`, grouped, with a currency symbol) and ISO→local dates for the
  earnings/invoices/PeriodPay surface, reusing the Core currency-symbol resolution. *Collaborators:* the Core
  currency-symbol utility (`NumberFormatter`/`Locale`); none else (pure deterministic transforms). *Does NOT know:* the
  DTOs, which screen consumes it, the dashboard's `DashboardFormat.money` (a separate `%.0f` helper it does NOT
  overload), or the server amounts' authority.
- **`ios-earnings-summary-vm`** (new, partner earnings feature) — the `EarningsSummaryViewModel.kt` parity (decision (d)):
  *responsibility:* load the earnings summary (a sealed `UiState<DashboardStatsDto>`) by REUSING
  `PartnerDashboardClient.getStats`; surface errors via the `SnackbarController`. *Collaborators:* `PartnerDashboardClient`
  (via the ADR-0019 spine), `SnackbarController`, `EarningsFormat` (in the view). *Does NOT know:* the payroll client,
  `GetPeriodPays`, the invoices list, or who the caller is (the server scopes the own-stats read — SECURITY §7.11).
- **`ios-invoices-vm`** (new, partner invoices feature) — the `InvoicesListViewModel.kt`/`InvoiceDetailViewModel.kt`
  parity (decisions (a)/(b)): *responsibility:* load the invoices list (sealed per-list `UiState<[EmployeeInvoiceDto]>`
  + a `RefreshPhase` enum + the ported staleness watermark) and one invoice detail (sealed `UiState`); on "Open PDF",
  call the generated `downloadInvoice` and surface the returned local file URL via a ONE-SHOT event (guarded on
  `pdfGenerationFailed`). *Collaborators:* the payroll generated client (via the ADR-0019 spine), `SnackbarController`,
  `QuickLookPreview` (via the screen), `EarningsFormat` (in the view). *Does NOT know:* the `QLPreviewController`
  internals, how the file was written (the codegen), who may read the invoice (SECURITY §7.11), or the audience router
  (it pushes within the `.invoices` tab's stack — the `EarningsRoute` enum).
- **`ios-periodpay-vm`** (new, partner payroll feature) — the `PeriodPayViewModel.kt` parity (decisions (a)/(c)):
  *responsibility:* load one period's pay rollup (a sealed `UiState<PeriodPaySummary>`) via the generated
  `employeePayrollGetPeriodPays`; format rows with the route-threaded `currencyCode` via `EarningsFormat`. *Collaborators:*
  the payroll generated client (via the ADR-0019 spine), `SnackbarController`, `EarningsFormat`. *Does NOT know:* the
  invoices list/detail, how the caller's own `employeeId` is trusted on the wire (SECURITY §7.11 E1 — iOS has no
  `UserProfileStore`, the server scopes the read), or the audience router.

#### Test contract (T-0309 — red-first)

- **TC-IOS-EARNINGS-NAV** (decision (a)): `onOpenEarnings` sets `ShellModel.selection = .invoices` (a tab switch, NOT a
  push onto the Dashboard tab's stack); the `.invoices` tab's `NavigationStack` is seeded with `EarningsRoute.summary`
  (the root), and "View all invoices" pushes `.invoices`, an invoice card pushes `.invoiceDetail(id)`, "View period pay"
  pushes `.periodPay(payPeriodId, currencyCode)` (the currencyCode threaded from the invoice).
- **TC-IOS-PDF-GATE** (decision (b)): an invoice with `pdfGenerationFailed == true` renders the Open-PDF affordance
  disabled/hidden; with `pdfGenerationFailed == false`, tapping Open-PDF calls `downloadInvoice` and emits a one-shot
  URL event (the screen presents `QuickLookPreview` over it); a download error (non-flag) snackbars and does NOT present.
- **TC-IOS-EARNINGS-FORMAT** (decision (c), pure): `EarningsFormat` formats `1234.5` as `1,234.50 <sym>` (decimal) and
  `1234.5` as `1,235 <sym>` (whole, the `%,.0f` rounding); an unknown currency code falls back to the raw code (never
  crashes); a nil symbol yields the bare grouped number.
- **TC-IOS-EARNINGS-STATS** (decision (d)): the Earnings summary VM loads via `PartnerDashboardClient.getStats`
  (asserted — NOT the payroll client, NOT `GetPeriodPays`); a `.success` → `.loaded(DashboardStatsDto)`, a `.failure` →
  `.error` + snackbar (the `EarningsSummaryViewModel.kt:44-59` parity).

(The SECURITY ownership test **TC-IOS-EARNINGS-OWNERSHIP** + the E4 PDF-cleanup test are §7.11's — they compose with
TC-IOS-PDF-GATE: the same `QuickLookPreview` coordinator that this record shapes is where E4's remove-on-dismiss runs.)

---

### 7.13 T-0311 (Phase-5 partner APNs push REGISTRATION + token plumbing + device lifecycle + minimal foreground/tap) — acceptance scope + the three Understand-pass rulings (recorded 2026-06-28, architect)

The partner **APNs push registration** surface — register for remote notifications → an APNs device token → POST it to the
**same `/api/Device/*` contract** the Android `:core` push uses (`Platform="ios"`, the **one** `X-Device-Id`) and re-register
on login / clear on logout — surfaced by the T-0311 Understand pass on `phase/ios-phase5` (depends_on T-0302/0303/0310/0331).
**Scope = REGISTRATION + token plumbing + device lifecycle + a MINIMAL foreground-banner / tap-to-route.** The **in-app
notifications feed + persistence + dashboard bell badge + title/body templates + channels are DEFERRED → T-0336** (the spike,
already filed; §7.7 / living doc). The generated **`PartnerDeviceAPI`** already carries
`deviceRegister(RegisterDeviceCommand{deviceId, deviceToken, platform})` / `deviceUnregister(deviceId)` — both ride the
**ADR-0019 spine** (the Bearer + `X-Device-Id`/`X-Device-Label`/`X-Time-Zone` + single-flight 401-refresh, via the
`RequestBuilderFactory`); T-0311 writes **no auth code**, it just calls them. **The Android parity is the well-factored
`:core` push** (`PushTokenRepository.kt`, `PushTokenSessionObserver.kt`, `DeviceRegistrationClient.kt`,
`AuthRepository.kt:210-225`).

**Three rulings, all APPLYING accepted ADRs + prior records — NO new ADR.** (a) APPLIES the **Core-seam family** precedent
— ADR-0014 D6′ (the system-framework-behind-a-Core-protocol idiom: `MapProvider`/`MKMapView`, the `LocationProvider`) +
ADR-0018 D2 (the brand-skin-over-native idiom: `CameraOrLibraryPicker`/`QuickLookPreview`) — the `PushRegistrar` is the
next member of that family. (b) APPLIES the **ADR-0019** spine (the generated `PartnerDeviceAPI` on the
`RequestBuilderFactory`) + the existing **`SessionScopedCacheRegistry`** + the `AuthApiClient.logout()` ordering. (c) is a
scope/parity confirmation (the §7.6 D3 "sealed-state absence is correct" precedent + the §7.7 dropped-Notifications-row
precedent). **A genuinely-new trade-off requiring a new ADR was looked for and NOT found** — the seam shape, the
session-driven lifecycle, the device contract, and the deferral are all applications of records already accepted. **The
registration / logout-clear-ordering SECURITY gate is ruled in PARALLEL by the security charter (Gate-SEC) — out of this
record's scope; this record fixes the architectural seam + names WHERE `unregisterDevice()` is invoked so the security
ordering has a home, not the ordering's enforcement.** Android source mirrored:
`core/.../notifications/{PushTokenRepository,PushTokenSessionObserver,DeviceRegistrationClient,PushTokenDataStore}.kt` +
`partner-app/.../data/auth/AuthRepository.kt:210-231`.

**Two build slices (as the brief scoped them):** **Slice A** = the `PushRegistrar` Core seam + the `Device/Register` client
binding + the registrar logic (the `PushTokenRepository` parity); **Slice B** = the lifecycle wiring (the
`PushSessionObserver` + `@UIApplicationDelegateAdaptor`) + the minimal foreground/tap + the `aps-environment` entitlement.

#### Decision (a) — the `PushRegistrar` Core seam + the `@UIApplicationDelegateAdaptor` AppDelegate hook (APPLIES ADR-0014 D6′ + ADR-0018 D2 — the Core-seam family; NO new ADR; HARVEST to `patterns-mobile`)

**RULING (CONFIRMED as recommended): a `PushRegistrar` protocol in `CleansiaCore/Push` is the SOLE consumer of
`UserNotifications` (`UNUserNotificationCenter`) + `UIApplication.registerForRemoteNotifications` — feature/lifecycle code
imports neither `UserNotifications` nor `UIKit`** (exactly as feature code imports neither `MapKit`/`CoreLocation` behind
`MapProvider`/`GeocodingService`, nor the `UIImagePickerController`/`QLPreviewController` behind
`CameraOrLibraryPicker`/`QuickLookPreview`). **The SwiftUI-App lifecycle hook for the APNs-token callbacks
(`application(_:didRegisterForRemoteNotificationsWithDeviceToken:)` / `...didFailToRegister...`) is a `@UIApplicationDelegateAdaptor`-installed
`AppDelegate`** — the canonical SwiftUI way to receive AppDelegate callbacks in an App-lifecycle app (SwiftUI's `App`
struct exposes no native `didRegisterForRemoteNotifications` hook; `@UIApplicationDelegateAdaptor` is Apple's sanctioned
bridge, available on the iOS-16 floor — ADR-0014). This APPLIES the existing seam-family records; record, **no new ADR**.
HARVEST the seam into `patterns-mobile` as the canonical "iOS push — the ONE way."**

- **The seam shape (`PushRegistrar` protocol in `CleansiaCore/Push`).** It exposes exactly three things, mirroring the
  three jobs the Android `:core` push splits across its repo + the messaging service:
  1. **`requestAuthorization() async -> Bool`** — wraps `UNUserNotificationCenter.current().requestAuthorization(options:)`
     (the iOS sibling of Android's `POST_NOTIFICATIONS` runtime grant). Decision (c) rules the rationale/soft-ask: **skip**
     for strict Android parity.
  2. **`registerForRemoteNotifications()`** — calls `UIApplication.shared.registerForRemoteNotifications()` (must run on the
     main actor) — the iOS analogue of FCM minting a token; iOS asks APNs, the OS delivers the token via the AppDelegate.
  3. **an APNs-token stream the AppDelegate FEEDS** — a `@Published`/`AsyncStream`/`PassthroughSubject` `apnsToken: String?`
     the `@UIApplicationDelegateAdaptor` `AppDelegate` writes on `didRegisterForRemoteNotificationsWithDeviceToken` (the
     `Data` token hex-encoded to the string the backend stores). **This is the structural parity to
     `PushTokenRepository.fcmToken: StateFlow<String?>`** (`PushTokenRepository.kt:55`) — a hot stream the observer reacts
     to, fed out-of-band by the OS callback, so the registrar never juggles the AppDelegate callback directly. (The
     `didFailToRegister` callback logs best-effort and leaves the stream nil — the Android `fetchTokenOrNull` swallow-and-log
     parity, `PushTokenRepository.kt:147-163`.)
- **Why a Core seam (the recorded Gate-DP/Core divergence — FCM→APNs over the SAME `Device/*` contract — ADR-0018 D8).**
  Android uses **FCM** (`FirebaseMessaging.getInstance().token` + the messaging-service `onNewToken`); iOS swaps to **APNs**
  (`UIApplication.registerForRemoteNotifications` + the AppDelegate `didRegister…DeviceToken` + `UNUserNotificationCenter`)
  — but **both POST the identical `Device/*` contract** (`deviceId`/`deviceToken`/`platform`), iOS sending **`Platform="ios"`**
  where Android sends `"android"`. **This is the ADR-0013 D8 push divergence — a Gate-DP divergence with architect sign-off,
  NOT a new trade-off:** *"Android FCM (`FirebaseMessaging` token + `onNewToken`) → iOS APNs (`registerForRemoteNotifications`
  + the `@UIApplicationDelegateAdaptor` `didRegister…DeviceToken` + `UNUserNotificationCenter`); the SAME `Device/Register`/
  `Device/Unregister` contract, `Platform="ios"`; the mechanism is the native platform push transport, the contract +
  lifecycle are identical."* The `PushRegistrar` is what isolates that transport swap behind one Core protocol — so the
  customer app (a future wave) installs the same seam with its own `Platform="ios"` and its own `CleansiaCustomerDeviceAPI`,
  and a forced future transport change (a new APNs API, a re-platform) migrates **one provider, not every feature**.
- **Why `@UIApplicationDelegateAdaptor` (vs the rejected alternatives).**
  - **A bare SwiftUI `.onReceive`/scene-phase hook** — REJECTED: SwiftUI's `App`/`Scene` exposes **no**
    `didRegisterForRemoteNotificationsWithDeviceToken`; that callback is an `UIApplicationDelegate` method only. There is no
    SwiftUI-native receiver — `@UIApplicationDelegateAdaptor` is the **only** sanctioned bridge and the documented Apple way.
  - **A hand-rolled `UIApplicationDelegate` set via `UIApplication.shared.delegate`** — REJECTED: fighting the SwiftUI
    App-lifecycle (which owns the delegate); `@UIApplicationDelegateAdaptor` is the supported, lifecycle-integrated form.
  - **`@UIApplicationDelegateAdaptor` feeding the Core `PushRegistrar`'s token stream** — CHOSEN: the canonical SwiftUI
    AppDelegate bridge, thin (it only forwards the OS callbacks into the Core stream), and it keeps `UIKit`/`UserNotifications`
    out of features — the App target's AppDelegate is the one allowed `UIKit` touch-point, exactly as the App target is the
    one place that *installs* the `RequestBuilderFactory` (ADR-0019) and the `MapProvider` (§7.6).
- **The seam stays in `CleansiaCore/Push`, the AppDelegate stays in the App target.** The `PushRegistrar` protocol + its
  default impl (`UNUserNotificationCenter`/`UIApplication` consumer) are Core (shared by partner now, customer later — the
  "shared code in Core, never duplicate across the two apps" rule, the same reason `QuickLookPreview`/`CameraOrLibraryPicker`
  are Core). The `@UIApplicationDelegateAdaptor` `AppDelegate` is **per-app** (it lives in the App target and feeds the Core
  registrar's stream) — the App-target-owns-composition split (ADR-0013 D3), not a Core type.

#### Decision (b) — the lifecycle-wiring home: a Core `PushSessionObserver` (true `PushTokenSessionObserver` parity); `unregisterDevice()` invoked from `AuthApiClient.logout()` BEFORE the token wipe; the local clear via `SessionScopedCache` (APPLIES ADR-0019 spine + the `SessionScopedCacheRegistry` + the Android `AuthRepository.kt:210-225` ordering; NO new ADR)

**RULING (CONFIRMED as recommended): register-on-login / clear-on-logout is a Core **`PushSessionObserver`** attached from
the App — the true `PushTokenSessionObserver.kt` parity — NOT an ad-hoc hook bolted onto `PartnerRootView.afterLogin` +
the logout call.** Registration is a **PROPERTY of the session×token state, not an event**: the observer combines the
session-presence stream (the `TokenStore`/`SessionManager` validity) with the registrar's APNs-token stream, drops emissions
where either is nil, dedupes, and calls the registrar's `ensureRegistered` on every distinct pair — the
`combine(session, token).filterNotNull().distinctUntilChanged() → ensureRegistered` shape verbatim
(`PushTokenSessionObserver.kt:56-64`). **The unregister DELETE is invoked from `AuthApiClient.logout()` BEFORE the access
token is wiped** (the call needs the Bearer); the **local cache `clear()` runs via `SessionScopedCache` on sign-out**
(both the user-logout path and the forced-401-sign-out path). Record, **no new ADR** (it APPLIES the ADR-0019 spine, the
existing `SessionScopedCacheRegistry`, and the Android ordering).

- **Why the observer, not the event hooks (the parity + the App-stays-thin argument).** The Android `:core` rewrote this
  EXACTLY because the event-hook approach (register on login + on email-confirm + on FCM rotation) was the source of
  "device wasn't registered" bugs — each hook could be missed (cold-launch on an existing session never re-registers;
  rotation while signed-out silently 401s; pre-FCM installs never registered) (`PushTokenSessionObserver.kt:12-40`, the
  docstring). Hooking iOS's `afterLogin` + the logout call would **re-introduce the exact event-hook brittleness the
  Android team deleted** — and it would not cover the cold-start-on-existing-session case (the partner app re-launches into
  an authed session via the SplashGate; no `afterLogin` fires) nor APNs-token-arrives-after-login (the AppDelegate callback
  is async). The observer makes registration a state property → all four cases (cold-start-authed, login, token-arrives,
  logout-drops) fall out for free. It also keeps the App thin: the App `attach`es one observer (the
  `MainActivity.onCreate → observer.attach(lifecycleScope)` parity, `PushTokenSessionObserver.kt:38-39,53`), it does not
  carry registration logic.
- **The `ensureRegistered` cache short-circuit is PORTED (load-bearing, not optional).** `PushRegistrar.ensureRegistered`
  must short-circuit on a locally-persisted "last registered token" (`PushTokenRepository.ensureRegistered:125-135` —
  `if token == readLastRegisteredToken() return`) so re-attaching on every cold start is **free** (no redundant
  `Device/Register` round-trip). It **persists on success only** (`writeLastRegisteredToken` after `register` returns ok —
  the `DeviceRegistrationClient.register` "true only when the backend accepted" contract, `DeviceRegistrationClient.kt:8-16`).
  The persistence is **`UserDefaults`-backed, NOT Keychain** (the Android `PushTokenDataStore`/DataStore parity — the
  last-registered-token is not a secret and may reset on reinstall, like `AppSettingsStore`, §7.5 D1 / #26a). **A secret
  reaching this store, or the device-id resolved anywhere but the one `DeviceIdProvider`, is a finding.**
- **The unregister ordering — WHERE it is invoked (the load-bearing security-owned constraint, given an architectural
  home).** The Android ordering is exact and verified (`AuthRepository.kt:210-225`): `logout()` calls
  `pushTokenRepository.unregisterDevice()` **first** (best-effort, `runCatching`), **then** `authLogout(refreshToken)`,
  **then** `signOutLocal()` (which wipes the `TokenStore` + clears the `SessionScopedCache`s). **The iOS home for the
  invocation is `AuthApiClient.logout()`** (the spine's logout, the `authClient.logout()` the brief names) — it calls the
  registrar's `unregisterDevice()` **before** it wipes the `TokenStore`, because the `Device/Unregister` DELETE rides the
  ADR-0019 spine and **needs the Bearer** (a wiped token → a tokenless 401 → the row is not deleted server-side until it
  GC's on the next NotRegistered report). It is **best-effort** (a failure still proceeds to the local wipe — the Android
  `runCatching` parity, `AuthRepository.kt:215`). **The local `clear()`** (drop the persisted last-registered-token so the
  next user on this handset re-registers fresh) is the **`SessionScopedCache.clear()`** the registrar's
  store/repo implements, run by the `SessionScopedCacheRegistry.clearAll()` on **both** sign-out paths (user logout AND the
  forced-401 sign-out) — exactly as the `PushTokenRepository` implements `SessionScopedCache` (`PushTokenRepository.kt:44,65-67`
  — `clear()` is local-only, never a network call, because the forced sign-out happens after the token is already dead and a
  network unregister would 401). **The unregister-network-DELETE (ordering + best-effort) is invoked from `logout()`; the
  local-cache wipe is the `SessionScopedCache` on every sign-out.** **The SECURITY charter rules the gate** (that the DELETE
  precedes the wipe; that no logout path skips it; that the forced-sign-out path clears local) **in parallel (Gate-SEC) —
  this record only fixes that the invocation HOME is `AuthApiClient.logout()` (before the wipe) + the `SessionScopedCache`
  (local), so security's ordering rule has a defined seam to attach to.**
- **Rejected: the `PartnerRootView.afterLogin` + logout-call hooks.** Re-introduces the event-hook brittleness the Android
  `:core` deleted (misses cold-start-authed + token-arrives-after-login), thickens the App with registration logic, and
  forks two clear-paths (logout vs forced-401) that can drift — the exact reason the `SessionScopedCacheRegistry` exists
  (one clear, two callers). The observer + the spine-`logout()` invocation is the parity-correct, drift-proof home.

#### Decision (c) — foreground/tap scope + the permission/rationale + the no-`UiState` confirmation (CONFIRM: minimal now, feed → T-0336; NO plist key; skip the rationale string; the sealed-state ABSENCE is correct)

**RULING (CONFIRMED as recommended):** ship a **MINIMAL `UNUserNotificationCenterDelegate`** now — `willPresent` (the
foreground banner) + `didReceive` (tap → resolve to the existing order route via a `PartnerNotificationDeepLink` port) —
and **DEFER the in-app feed + persistence + dashboard bell badge + title/body templates + channels → T-0336**. **NO Info.plist
purpose string** (APNs does not require `NSUserNotificationsUsageDescription` — the OS shows the system permission alert
itself; only the `aps-environment` entitlement is required). **SKIP the soft-ask rationale string for strict Android parity**
(Android requests `POST_NOTIFICATIONS` silently, no rationale screen). **NO `UiState`/`ActionState`** — push registration is
fire-and-forget background plumbing; the sealed-state ABSENCE is correct, not a reviewer finding. Record, no new ADR.

- **Foreground/tap minimal scope.** `willPresent` shows the foreground banner (the iOS default-suppressed-in-foreground
  behavior must be opted into — `[.banner, .sound]` / `[.list]`); `didReceive` reads the tap's userInfo and routes via a
  **`PartnerNotificationDeepLink` port** to the **existing** order route (the `EarningsRoute`/order-detail destinations
  T-0307/T-0309 already built — the tap does NOT invent a screen). The port is a thin Core protocol (the deep-link resolver)
  so the feature owns "what an order tap navigates to" and the delegate owns "a tap happened." **DEFERRED → T-0336:** the
  in-app notifications feed, local persistence of received pushes, the dashboard bell badge, server-driven title/body
  templates, and notification channels/categories. This mirrors the §7.7 ruling that the partner "Notifications" surface is
  a separate spike once a backend contract exists; T-0311 plumbs the OS path, T-0336 builds the in-app product.
- **No plist purpose string (the verified Apple fact — composes with ADR-0016 §7.x / the §A.0 purpose-strings note).**
  Remote push via APNs requires the **`aps-environment` entitlement** (`development`/`production`) + the runtime
  `requestAuthorization` (the OS shows its own system alert) — it does **NOT** require an `Info.plist`
  `NSUserNotificationsUsageDescription` key (no such purpose-string requirement exists for notifications, unlike
  location/camera/photo-library which DO need their plist keys, §7.6/§7.10). **Slice B ships the `aps-environment`
  entitlement** (`development` for the dev build; the `production` flip is part of the owner provisioning gate, below). The
  reviewer's purpose-strings check (ADR-0016) is satisfied by **no notifications plist key + the entitlement present** — a
  notifications plist key would be wrong (a non-existent requirement), and a missing entitlement is the finding.
- **Skip the rationale string (strict parity; the one optional fallback recorded).** Android requests `POST_NOTIFICATIONS`
  silently (no pre-permission rationale screen). **Strict parity = skip the soft-ask** — request authorization directly at
  the parity moment Android does (post-login). **IF a soft-ask is later wanted** (an "iOS does it right" enhancement, a
  Gate-DP-class component improvement), it is **one optional `.xcstrings` key ×5** (en/cs/sk/uk/ru) for a pre-permission
  explainer sheet — recorded as the bounded fallback, **not built in T-0311**. The recommendation is **skip**; if the owner
  later wants the soft-ask, it is the one key, not a redesign.
- **The no-`UiState`/`ActionState` confirmation (so a reviewer does NOT flag it — the §7.6 D3 AddressPicker precedent).**
  Push registration is **fire-and-forget background plumbing** — the registrar/observer have **no screen, no load-fetch
  (E1), no user-driven mutation (E2)**. Like the AddressPicker (§7.6 D3 — "an interactive map with plain `@Published` state,
  neither an E1 load-fetch nor an E2 mutation screen, so the sealed-state absence is correct"), the `PushRegistrar` /
  `PushSessionObserver` correctly have **no sealed `UiState<T>`/`ActionState`** — plain async functions + the token stream +
  best-effort logging. **The sealed-state archetypes are correctly scoped OUT; flagging their absence is a reviewer
  mis-fire, not a finding.** (The minimal foreground banner / tap-route is delegate-driven OS UI, not a VM-rendered screen —
  also correctly stateless. The in-app feed, T-0336, IS an E1 screen and WILL be sealed-state.)

#### Owner gate — T-0342 (APNs auth key + Push capability/provisioning) is the END-TO-END-DELIVERY gate (the T-0325-gates-T-0335 pattern)

**T-0311 ships CODE-COMPLETE + the `aps-environment` entitlement WITHOUT T-0342; delivery (a push actually arriving on a
device) is OWNER-GATED.** End-to-end push delivery requires owner-only setup: the **APNs auth key (`.p8`)** uploaded to the
backend's push sender, the **Push Notifications capability** enabled on the App ID, and the **provisioning profile** carrying
it. That is filed as the owner ticket **T-0342** *(owner: APNs `.p8` key + Push capability/provisioning)* — **NOT "T-0341"**:
the Understand pass proposed "T-0341" but **T-0341 is already taken** (the backend status-history flaky-test ticket), so the
APNs owner ticket is the next free number, **T-0342**. (The orchestrator creates the T-0342 file; this record references it +
the gate.) **The gate is the same shape as T-0325-gates-T-0335** (the location plist key the owner provides gates the
my-location FAB): T-0311 is **code-complete + entitlement-present** through the agent workflow; the live-delivery proof
(a test push round-trips to a device) is **deferred until T-0342 lands**. T-0311's reviewer/security gates do **not** block
on T-0342 (they verify the code seam + the entitlement, not a live push); the *delivery* acceptance is the owner's once
T-0342 is done. (Also note the standing owner-blocker list §7.x already names "APNs auth key / push certificate" for T-0311 —
T-0342 is that blocker's ticketized form.)

#### The recorded Gate-DP / Parity divergences (T-0311 — all transport/mechanism, none touch the device contract or lifecycle)

1. **Transport: Android FCM (`FirebaseMessaging` token + the messaging-service `onNewToken`) → iOS APNs
   (`UIApplication.registerForRemoteNotifications` + the `@UIApplicationDelegateAdaptor` `didRegister…DeviceToken` +
   `UNUserNotificationCenter`)** — decision (a); the **ADR-0013 D8** push divergence. The SAME `Device/Register`/
   `Device/Unregister` contract, **`Platform="ios"`**, the **one** `X-Device-Id` (== `DeviceIdProvider`, == the
   `Device/Register` deviceId, T-0331/T-0310 D6); the mechanism is the native platform push transport, the contract +
   register/clear lifecycle are identical.
2. **Permission: Android `POST_NOTIFICATIONS` runtime grant (silent, no rationale) → iOS
   `UNUserNotificationCenter.requestAuthorization` (the OS shows its own system alert)** — decision (c). No rationale screen
   (strict parity); **no Info.plist purpose string** (APNs needs only the `aps-environment` entitlement, unlike
   location/camera/photo which need plist keys); the one optional soft-ask `.xcstrings` key is the recorded, un-built fallback.
3. **Token rotation: Android's `onNewToken` messaging-service callback feeds `reportRotatedToken` → iOS's
   `didRegisterForRemoteNotificationsWithDeviceToken` (re-fired by the OS on token change) feeds the registrar's APNs-token
   stream** — decision (a)/(b); same "rotation is just a new emission the session observer re-registers on" property, native
   callback. (No Firebase-project migration analogue — `PushTokenRepository.runFirebaseProjectMigrationOnce` is FCM-specific,
   correctly NOT ported.)

#### Reviewer-check addition (T-0311+)

- **#34 (§7.13 (a)/(b)/(c)) — the iOS partner APNs push-registration surface is the canonical shape.**
  **(a) seam** — `UNUserNotificationCenter` + `UIApplication.registerForRemoteNotifications` are reached ONLY through the
  `CleansiaCore/Push` **`PushRegistrar`**; the APNs-token AppDelegate callbacks are received via a per-app
  **`@UIApplicationDelegateAdaptor`** that FEEDS the registrar's token stream. **Findings:** a feature/VM/lifecycle file
  `import UserNotifications`/`import UIKit` for push (the seam is the only consumer — the `MapProvider`/`CameraOrLibraryPicker`
  parity); a second push consumer outside `PushRegistrar`; a hand-rolled `UIApplication.shared.delegate` instead of
  `@UIApplicationDelegateAdaptor`; the device token POSTed with anything but `Platform="ios"` or a device id not from the
  one `DeviceIdProvider`. **(b) lifecycle** — register/clear is the Core **`PushSessionObserver`** (the
  `combine(session, token).filterNotNull().distinctUntilChanged() → ensureRegistered` parity, attached once from the App);
  `ensureRegistered` short-circuits on the persisted last-registered token (`UserDefaults`, not Keychain) and persists on
  success only; **`unregisterDevice()` is invoked from `AuthApiClient.logout()` BEFORE the `TokenStore` wipe** (best-effort)
  and the local `clear()` is the `SessionScopedCache` on every sign-out. **Findings:** registration bolted onto
  `afterLogin`/an event hook instead of the session-state observer (the brittleness the Android `:core` deleted); the
  last-registered-token in the Keychain (it is not a secret) or a secret in its store; `unregisterDevice()` after the token
  wipe (a tokenless 401, the row not deleted) or skipped on a logout path; a second clear-path not going through the
  `SessionScopedCacheRegistry`. **(SECURITY rules the unregister-ordering GATE in parallel — Gate-SEC — #34 verifies the
  seam/home, not the security mandate.)** **(c) scope/permission** — minimal `willPresent` (foreground banner) + `didReceive`
  (tap → existing order route via the `PartnerNotificationDeepLink` port) only; the in-app feed/badge/persistence/templates
  are DEFERRED → T-0336; the `aps-environment` entitlement is present; there is **no** notifications Info.plist purpose
  string; **no** `UiState`/`ActionState` on the registrar/observer (correctly stateless — do NOT flag). **Findings:** an
  in-app feed / bell badge / push persistence built in T-0311 (it is T-0336); a notifications plist purpose string (a
  non-existent requirement); a missing `aps-environment` entitlement; a flagged-as-missing `UiState`/`ActionState` on the
  registrar/observer (the §7.6 D3 mis-fire). (Composes with #13-gen — the `Device/*` calls ride the ADR-0019 spine.)

#### New CRC roles (added with the T-0311 wiring; the RDD home is the planned `agents/knowledge/roles/ios-*` cards that land with their code — ADR-0019 precedent)

- **`ios-push-registrar`** (new, `CleansiaCore/Push`) — the `PushRegistrar` protocol + its default impl (decision (a); the
  next member of the ADR-0014 D6′ / ADR-0018 D2 system-framework-behind-a-Core-seam family): *responsibility:* be the SOLE
  consumer of `UNUserNotificationCenter` (authorization) + `UIApplication.registerForRemoteNotifications`, expose an
  APNs-token stream the AppDelegate feeds, and `ensureRegistered(token)` it to `Device/Register` (cache-short-circuit +
  persist-on-success) / `unregisterDevice()` it from `Device/Unregister` — the `PushTokenRepository.kt` parity over APNs.
  *Collaborators:* the generated `PartnerDeviceAPI` (via the ADR-0019 spine), the one `DeviceIdProvider`, the persisted
  last-registered-token store (`UserDefaults`), the `SessionScopedCacheRegistry` (it implements `SessionScopedCache.clear()`
  local-only). *Does NOT know:* the access token value (that is `TokenStore`'s, reached only via the spine), how a session
  becomes present (that is the observer's), which screen a tap routes to (that is the `PartnerNotificationDeepLink` port's),
  the notification's title/body/template (T-0336), or any FCM/Firebase concept. **If this role reads the access token, or
  resolves a device id anywhere but `DeviceIdProvider`, the responsibility is wrong — it delegates to the spine** (the
  ADR-0019 invariant; the remote-revoke-correctness reason for the one device id).
- **`ios-push-session-observer`** (new, `CleansiaCore/Push`) — the `PushSessionObserver` (decision (b); the
  `PushTokenSessionObserver.kt` parity): *responsibility:* make registration a PROPERTY of session×token state — combine
  session-presence with the registrar's APNs-token stream, drop nils, dedupe, and call `ensureRegistered` on every distinct
  pair; attached once from the App. *Collaborators:* the `TokenStore`/`SessionManager` (session-presence stream), the
  `PushRegistrar` (the token stream + `ensureRegistered`). *Does NOT know:* how to talk to APNs or the backend (that is the
  registrar's), the token value, the unregister/logout ordering (that is `AuthApiClient.logout()`'s — decision (b)), or any
  screen. **If this observer registers on a discrete event (login/confirm) instead of the combined state, the responsibility
  is wrong** — it re-introduces the event-hook brittleness the Android `:core` deleted.
- **`ios-push-app-delegate`** (new, per-app App target, via `@UIApplicationDelegateAdaptor`) — the thin AppDelegate +
  `UNUserNotificationCenterDelegate` (decisions (a)/(c)): *responsibility:* receive the OS push callbacks
  (`didRegisterForRemoteNotificationsWithDeviceToken` → feed the registrar's token stream; `didFailToRegister` → log;
  `willPresent` → foreground banner; `didReceive` → resolve the tap via the deep-link port) and forward them — it holds NO
  logic of its own. *Collaborators:* the Core `PushRegistrar` (feeds its token stream), the `PartnerNotificationDeepLink`
  port (resolves a tap to an order route). *Does NOT know:* the token contract / backend (the registrar's), the session
  state (the observer's), or how a screen renders (the feature's). The one allowed `UIKit`/`UserNotifications` touch-point in
  the App target (the composition-root parity — like installing the `RequestBuilderFactory`/`MapProvider`).
- **`ios-partner-notification-deep-link`** (new, port — Core protocol, partner-feature impl) — the tap→route resolver
  (decision (c)): *responsibility:* map a tapped notification's payload to an EXISTING order route (the T-0307/T-0309
  destinations); *Collaborators:* the partner router/route enum. *Does NOT know:* the OS delegate (the AppDelegate calls it),
  push transport, or the in-app feed (T-0336). A thin port so the feature owns "where an order tap goes," the delegate owns
  "a tap happened."

#### Test contract (T-0311 — red-first)

- **TC-IOS-PUSH-REGISTER** (decision (a)/(b)): with a present session + an APNs token in the registrar's stream, the
  observer calls `Device/Register` with `{deviceId == DeviceIdProvider.deviceId, deviceToken == the APNs token,
  platform == "ios"}` through the ADR-0019 spine; a second identical (session, token) emission is a **no-op**
  (cache-short-circuit); the last-registered token persists on success only.
- **TC-IOS-PUSH-OBSERVER** (decision (b)): the `combine(session, token)` observer (i) fires on cold-start-into-an-authed-
  session (no `afterLogin` needed), (ii) fires when the APNs token arrives after login, (iii) buffers a token that arrives
  while signed-out and fires once the session becomes present, (iv) does NOT re-register on logout (session→nil drops the
  emission) — the `PushTokenSessionObserver.kt:26-36` behavioural properties, 1:1.
- **TC-IOS-PUSH-LOGOUT-ORDER** (decision (b); the architectural-seam half — the security GATE is §7.x SECURITY's):
  `AuthApiClient.logout()` invokes `unregisterDevice()` (the `Device/Unregister` DELETE) **before** the `TokenStore` is
  wiped (asserted call order — the DELETE carries the Bearer); a failing unregister still proceeds to the local wipe
  (best-effort); the `SessionScopedCache.clear()` (local last-registered-token drop) runs on BOTH the user-logout and the
  forced-401 sign-out paths via the registry.
- **TC-IOS-PUSH-TAP** (decision (c)): `didReceive` for an order notification resolves via the `PartnerNotificationDeepLink`
  port to the existing order route (it does NOT create a screen); `willPresent` returns the foreground-banner presentation
  options; no `UiState`/`ActionState` is involved (the stateless plumbing assertion — the absence is intended).
- **No live-delivery test in T-0311** — an actual push round-tripping to a device is the **T-0342 owner-gated** delivery
  proof (the entitlement + `.p8` + provisioning). T-0311's tests assert the code seam + the contract + the ordering, not a
  live APNs round-trip.

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
| **T-0326** | **Sign in with Apple (customer app, 4.8)** — present SIWA alongside Google + email on the customer sign-in surface; working authenticated session. **Q-IOS-04 RESOLVED (§7.14); the SIWA PRESENTATION + the ASAuthorization/nonce wiring are now FOLDED INTO T-0312 Slice C (§7.15 D2/D3/D6).** T-0326 reduces to the **compliance VERIFICATION** that the shipped customer sign-in offers SIWA equivalently to Google (AR-ACCT-2 / 4.8) + the live-sign-in acceptance once the owner provisions the Apple capability (T-0344) | S (was M) | **proposed** | ios | T-0312 | **owner: T-0344 (Apple capability + `Apple:BundleId`) + T-0343 (backend AppleAuth) — the LIVE-sign-in gate; the code/button ship in T-0312** | Phase 2 (customer; verifies the T-0312 SIWA surface) |
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

---

> **Q-IOS-04 RESOLVED 2026-06-28 → §7.14 below.** The prior open-question default (a `googleauth`-mirror `appleauth` endpoint) is now the architect ruling; verified by a parallel codebase survey + a security design gate (PASS-WITH-REQUIREMENTS). Backend work → **T-0343**; owner provisioning → **T-0344** (Apple) + **T-0345** (Google/IMP-1); a Google `email_verified` hardening follow-up → **T-0346**.

### 7.14 Q-IOS-04 — Sign in with Apple backend mechanism + Google/Apple auth enablement for Phase 6 (T-0312 customer shell + auth) (recorded 2026-06-28, architect)

> **Resolves Q-IOS-04.** The native iOS customer app must offer Sign in with Apple (App Store policy when offering Google). SIWA is **net-new** — zero Apple/SIWA references anywhere on Android, and the committed `customer-mobile-api.json` has **no `/api/Auth/AppleAuth` path** (`/api/Auth/GoogleAuth` is the only social-auth route). So Apple needs a **backend contract decision before iOS auth work** — this is it. Google, by contrast, **already works**: `GoogleAuth.Handler` + `GoogleTokenVerifier` + the `POST /api/Auth/GoogleAuth` action on the Customer Mobile host are in master (post-T-0105/T-0128 hardening), and the iOS port reuses them 1:1.

**D1 — SIWA backend = a new AppleAuth feature + IAppleTokenVerifier, mirroring GoogleAuth EXACTLY (the reusable pattern).** We do **not** invent a new auth shape. `src/Cleansia.Core.AppServices/Features/Auth/AppleAuth.cs` mirrors `GoogleAuth.cs`: `Command(IdentityToken, RawNonce, FirstName, LastName) : ICommand<JwtTokenResponse>`, a `BaseAuthValidator` with **shape-only** rules (identity is bound from verified claims, never the client), and a Handler injecting `IAppleTokenVerifier` + `ITokenService` + `ICartRepository` + `IUserRepository` + `IHostAudienceProvider` (the same small collaborator set — no handler smell). `src/Cleansia.Core.AppServices/Services/AppleTokenVerifier.cs` mirrors `GoogleTokenVerifier.cs` as the **sole** caller of Apple's JWKS/JWT path and the single S1 server-truth-identity boundary.

**D2 — the verification checks (the verifier, fail-closed like Google).** AppleTokenVerifier: (1) fetch+cache Apple JWKS at `https://appleid.apple.com/auth/keys`, select the JWK by the token-header `kid`; (2) verify the **RS256** signature; (3) `iss == "https://appleid.apple.com"`; (4) `aud == AppleConfig.BundleId` (the NATIVE app bundle id `cz.cleansia.customer` — a Services ID is a **web-only** construct, not used by the native ASAuthorization path), pinned exactly as GoogleTokenVerifier pins `aud` to `GoogleConfig.ClientId`; (5) `exp` (Apple identity tokens live ~10 min) + `iat`; (6) the **nonce binding** — `SHA256(rawNonce the client POSTed) == token.nonce` (anti-replay); then extract `sub`, `email`, `email_verified` into `AppleVerifiedClaims(Subject, Email, EmailVerified)`. **Fail closed:** empty `AppleConfig.BundleId` → null; ANY failure → null → the handler returns a uniform `BusinessErrorMessage.InvalidAppleUserToken` (S4, no enumeration leak), identical to the Google contract.

**D3 — account link/create + the takeover guard (closes the gap Google's hardening closed).** Identity binds ONLY from the verified `sub`/`email`, never the client fields. Look up by `claims.Email`. If a user exists and `user.AuthenticationType != AuthenticationType.Apple` → reject `InternalAuthTypeError` — an Apple login must **not** bind into an existing `Internal` (password) **or** `Google` account sharing that verified email (the verified-email-collision takeover the T-0105/T-0128 work closed for Google; Apple gets the identical guard). Existing Apple-typed + `IsActive` → reissue the JWT. No user → **create only when `claims.EmailVerified`** (reject an unverifiable email rather than provision it) via `User.CreateWithApple(email, firstName, lastName, appleSub)` — `AuthenticationType.Apple`, the stable `sub` stored in a new `User.AppleId` column (sibling to `GoogleId`), `IsEmailConfirmed = true`. Apple returns name/email reliably **only on first authorization**, so first-login name comes from the command (exactly as Google keeps the client display name); subsequent logins identify by `sub` and never expect name again. **JWT issuance is reused verbatim** — `tokenService.GenerateTokenAsync(...)` does not branch on `AuthenticationType`, so Apple users ride the identical platform-JWT spine and the existing refresh/audience rules.

**D4 — the Apple Sign-in KEY (.p8) is DEFERRED.** We adopt the **identity-token-only** design: verify the `identityToken`, **never exchange the `authorizationCode`** at `/auth/token`, never store an Apple refresh/access token. App Store 5.1.1 account-deletion only requires `/auth/revoke` when you HOLD an Apple token; holding none, there is nothing to revoke, so the `.p8`/Key ID/Team ID are entirely out of scope for both login and 5.1.1. The key is added later only if a future feature needs Apple-side token exchange/revocation — an **open risk**, not built now.

**D5 — Google is config-only for iOS (no backend code change).** `GoogleTokenVerifier` already pins `aud` to `GoogleConfig.ClientId` (empty → fails closed). The ONLY iOS requirement is that `Google:ClientId` be set to the **WEB/server** OAuth client id, and that the iOS GoogleSignIn-iOS `serverClientID` equal that same value — Google mints the ID token with `aud = serverClientID`, so the existing verifier accepts iOS Google sign-ins unchanged. This is the still-pending IMP-1 provisioning. (Optionally add the iOS client id to the allowed audiences — not required, not recommended.)

**D6 — iOS T-0312 consumption rides the existing Core spine.** Google via GoogleSignIn-iOS (SPM) → `result.user.idToken.tokenString` → `POST /api/Auth/GoogleAuth`. Apple via first-party `AuthenticationServices`: generate a random raw nonce, send `request.nonce = SHA256(rawNonce)` to Apple (HASHED) and the **RAW** nonce to the backend alongside the `identityToken` → `POST /api/Auth/AppleAuth`. Both return `JwtTokenResponse` in the JSON body (native clients, no cookie); iOS mirrors the Android response contract (`isEmailConfirmed==false || empty token => email-verify, not error`), then persists the tokens into the **same Keychain `TokenStore` single mutation path** (ADR-0019). **In T-0312:** the ASAuthorization + GoogleSignIn wiring, the nonce flow, the two POSTs, JwtTokenResponse handling, store-on-spine — all code + unit-tested against the mockable verifier. **Deferred:** live sign-in (owner-gated), any Apple token exchange/revoke, 5.1.1 in-app revoke.

#### Ship-vs-owner-gated split (the T-0311 / T-0325-gates-T-0335 pattern)

**Code + tests SHIP in Phase 6 with NO owner provisioning:** the AppleAuth feature, the IAppleTokenVerifier/AppleTokenVerifier seam (defaulting to an empty `AppleConfig.BundleId` so it **fails closed** exactly like GoogleTokenVerifier), `AppleConfig`, `User.CreateWithApple` + `AuthenticationType.Apple` + `User.AppleId`, the `AppleAuth` endpoint + `InvalidAppleUserToken` + i18n, and the iOS ASAuthorization/GoogleSignIn wiring — all merge against placeholder config with **stubbed verifiers** in the unit suite (the T-0128 precedent: live-signature/JWKS rejection deferred to the integration suite). **Strictly OWNER-GATED for LIVE sign-in:** (Apple) the owner enabling the Sign in with Apple capability on the `cz.cleansia.customer` App ID + the matching Xcode entitlement + `Apple:BundleId=cz.cleansia.customer`; (Google) the owner creating the Google Cloud project, the iOS OAuth client id (+ reversed-client-id URL scheme), the web/server OAuth client id, then populating backend `Google:ClientId` and the iOS `serverClientID`. This is the same gate shape as T-0342-gates-T-0311: reviewer/security gates verify the code seam + fail-closed behaviour, not a live sign-in; the live-delivery acceptance is the owner's once the client ids / capability are provisioned.

#### MANUAL_STEPs (owner)
1. **EF migration** for the new `User.AppleId` column (Claude does not run migrations).
2. **Spec + client regen** of `customer-mobile-api.json` + the iOS/Android clients after the `AppleAuth` endpoint + DTOs land.
3. **Apple Developer** — enable Sign in with Apple on the `cz.cleansia.customer` App ID (primary), add the Xcode entitlement, set `Apple:BundleId`. **No `.p8`, no Services ID, no domain verification.**
4. **Google Cloud Console** (IMP-1) — iOS client id (+ reversed-client-id Info.plist scheme), web/server client id, set `Google:ClientId` = web client id = iOS `serverClientID`.

#### Reviewer-check addition (Q-IOS-04)
- **AppleAuth/AppleTokenVerifier mirror the Google pattern and bind identity from verified claims only.** Verify: aud pinned to `AppleConfig.BundleId`, iss/exp/nonce all checked, no environment bypass, fail-closed on empty config; handler rejects `AuthenticationType != Apple` (both `Internal` and `Google` collisions), creates only on a verified email, reuses `tokenService.GenerateTokenAsync`; no identity token is ever logged; lookups stay tenant-filtered (no `IgnoreQueryFilters`). The `.p8`/code-exchange path is **absent** (identity-token-only).

---

### 7.15 T-0312 (Phase-6 — the FIRST customer feature: customer app shell SCAFFOLD + FULL auth incl. Google + Apple) — acceptance scope + the seven Understand-pass rulings (recorded 2026-06-28, architect)

T-0312 is the **first customer-app wave** (the partner app is feature-complete). It ships **(a)** the customer **root + shell SCAFFOLD** — a `CustomerRootView` flat-enum root-switch (the ADR-0020 *pattern*, copied not reused) gated by `.splash`, landing on a `CustomerShellView` (a 4-tab `TabView`: Home·Orders·Rewards·Profile + a **center Book FAB**) whose four tabs are placeholders (the T-0304 partner-shell-scaffold precedent) — and **(b)** the **FULL auth surface** — SignIn / SignUp / EmailVerify mirroring the partner T-0305 chain + the Android customer **event-driven `AuthViewModel`** (`SignedIn` / `NeedsEmailConfirm(email)` / `PasswordReset`), plus the **two social providers** (Google via GoogleSignIn-iOS, Apple via `AuthenticationServices`) per the §7.14 D6 consumption plan. The Book FAB is **present but its action (the booking wizard) is T-0313**; tab CONTENT is **T-0314**. Surfaced by the T-0312 Understand pass (`depends_on` T-0302 codegen, T-0306 — and **rides** the §7.14-mandated regen of `customer-mobile-api.json` for the new `AppleAuth`).

> **Six of the seven rulings APPLY accepted ADRs + prior records — NO new ADR.** (1) the root shape COPIES the **ADR-0020 pattern** (which D5/§7.4 explicitly reserved for the customer wave to *copy, not reuse*) with the customer's own audience enum — and **confirms the simpler customer gate** (NO RegistrationLock — that predicate is partner-only; verified absent in the Android customer `CleansiaNavHost.kt:137-147`). (2) the auth surface APPLIES the T-0305 chain + the Android customer `AuthViewModel` event contract; the social **placement** APPLIES **ADR-0018** (Gate-DP — native SwiftUI components, the official Apple button per HIG, iOS-wins-on-conflict) + **ADR-0016** AR-ACCT-2 (4.8 — SIWA offered equivalently). (3) the provider-code home APPLIES **ADR-0013 D1/D3** (shared spine in Core; provider-acquisition in the app target) + **ADR-0019** (the spine the POSTs ride) + the §7.14 D6 plan. (4) the scope/deferral map APPLIES the **T-0304 shell-scaffold precedent** + the T-0313/T-0314 dependency rows. (5) the regen-ahead split APPLIES the **T-0305/T-0309 "build the VM against a Core protocol + fakes, bind the generated client last"** precedent. (6) the Gate-DP divergences + the new SPM dep + the SIWA entitlement APPLY ADR-0018 + the §7.14 MANUAL_STEPs. **A genuinely-new trade-off requiring a new ADR was looked for and NOT found** — every choice composes an accepted decision. **(7) the slice plan** sizes T-0312 (M) into 3 slices. **The auth slices (B, C) are SECURITY-touching → Gate-SEC runs in PARALLEL** (the social-token → empty-token-gate → Keychain path); this record fixes the architectural seams, not the security enforcement. Android parity sources (verified): the customer shell `features/main/MainShell.kt` (the `MainTab{Home,Orders,Rewards,Profile}` enum `:66` + the floating-pill `CustomBottomBar` + the center `BookFab` `:363-474`); the root gate `navigation/CleansiaNavHost.kt:117-148` (`startDestination = Splash`, splash → `hasValidSession ? Home : SignIn` — **no RegistrationLock**) + the auth-outcome → nav wiring `:149-281`; the event-driven `features/auth/AuthViewModel.kt` (the `AuthOutcome{SignedIn,NeedsEmailConfirm,PasswordReset}` sealed set `:217-226`, the `toAuthUiState` empty-token mapping `:195-205`, `signInWithGoogle` `:142-173`); the social-button layout `features/auth/SignInScreen.kt:147-157` (the `OR` divider → social buttons); `core/auth/GoogleSignInController.kt` (the provider-acquisition shape: returns a typed result, never navigates).

> **On-disk reality (verified — the scaffold is already partly stood up by prior infra tickets):** `src/cleansia_ios/CleansiaCustomer/` exists (`CleansiaCustomerApp.swift`, `CustomerAppContainer.swift` wiring the `BaseAppContainer` + `CustomerAuthSpine`, `CustomerClients.swift`, a placeholder `ContentView.swift`); the generated `CleansiaCustomerApi/` exists with `GoogleAuthCommand.swift` + `JwtTokenResponse.swift` + `MobileLoginCommand.swift` but **NO `AppleAuthCommand.swift`** (the §7.14 regen dependency — confirmed absent); `AnonymousAllowList.customer` already carries `sharedAuth` (incl. `/api/auth/googleauth`) + the guest-booking paths but **NO `/api/auth/appleauth`** (added when AppleAuth ships — see Decision 3); the hand-written Core spine `AuthApiClient` (`Auth.swift`) already exposes the **shared `resolveEmailGate` empty-token gate** (`:175-188`) + the single Keychain `persist` path (`:292-301`) that the two social outcomes ride. The `CustomerRootView`, `CustomerShellView`, the auth screens/VMs, and the provider wiring are **net-new** in T-0312.

#### Decision 1 — Customer root/router shape: a `CustomerRootView` flat-enum root-switch (the ADR-0020 PATTERN, copied), splash-gated, with NO RegistrationLock — the SIMPLER customer gate (APPLIES ADR-0020 D5; NO new ADR)

**RULING (CONFIRMED): the customer app gets its OWN `CustomerRootView`, a flat-enum root-switch over a customer audience enum, gated by `.splash` — COPYING the ADR-0020 partner-router PATTERN, NOT reusing `PartnerRootView`. The gate is SIMPLER than the partner's: splash → (authed → main shell) / (anon → auth); there is NO RegistrationLock (partner-only).** ADR-0020 D5 explicitly reserved this: *"The customer app (T-0312+) gets its own root view with its own audience states (Home shell + Book FAB, Google/SIWA sign-in) — it copies this pattern, not the partner enum."* This is the cash-out of that reservation — a **record, not a new ADR**.

- **The customer audience enum (the partner enum MINUS the partner-only states).** `CustomerRootView.Route` is a closed `enum`:
  ```swift
  enum Route: Equatable {
      case splash                       // the decision state
      case login
      case register                     // (the customer SignUp screen — net-new; partner had it from T-0305)
      case forgotPassword
      case verifyEmail(email: String?)  // the §7.5 D2 associated-value pattern, reused verbatim
      case home                         // = the CustomerShellView (the 4-tab TabView + Book FAB)
  }
  ```
  **NO `.registrationLock`, NO `.onboarding`.** The RegistrationLock predicate (`hasCompletedProfile && areDocumentsUploaded && contract∈{Approved,Active}`, §7.4 Decision 1) is a **cleaner-eligibility** gate that does not exist for customers — verified: the Android customer `CleansiaNavHost.kt` splash resolves **directly** `hasValidSession ? Routes.Home : Routes.SignIn` (`:137-147`) with **no** registration-status call, and the customer `MainShell` does its own **soft** profile-completeness nudge **inside** the shell (the "missing phone → onboarding" `LaunchedEffect`, `MainShell.kt:156-181`) — a non-blocking in-shell prompt, **not** a hard router gate. **That soft nudge is T-0314's concern** (it needs the Profile/EditProfile screens that don't exist in T-0312); T-0312's `.home` lands the shell scaffold unconditionally for an authed session.
- **The seed + the splash resolve (the simpler tree).** Seed **unconditionally `.splash`** (the §7.5 ADR-0020 fold-in posture — splash is the sole launch resolver), which resolves: **valid session → `.home`**; **no/expired session → `.login`** (the Android `hasValidSession = tokenStore.current()?.let { !it.isRefreshExpired() } == true`, `CleansiaNavHost.kt:141`). There is **no status round-trip** on the customer splash (the partner's `employeeCheckCurrentEmployee` gate has no customer analogue) — so the customer `SplashGateViewModel` is a **thin session-presence resolver**, not a fail-closed registration gate. **Why splash at all if it only checks the token?** Parity + the seam: it keeps the launch-resolution single-homed (one place to add the T-0314 soft-onboarding branch later) and matches the Android `startDestination = Splash`; a `.refreshExpired` returning session still lands `.home` and the ADR-0019 401-refresh spine handles a stale *access* token transparently (the Android comment `:139-140`).
- **The bounce + the verified-login gate are PRESERVED (the security floor that DOES apply).** A verified login routes through `.splash` → `.home` (or, for an unverified token-bearing session, → `.verifyEmail` — the §7.2/§7.5 D3 empty-token gate, which **does** apply to customers: a `200 + empty/blank Token` or `isEmailConfirmed == false` must **not** land `.home`). `Route.afterLogin(_ success:)` returns `success.requiresEmailConfirmation ? .verifyEmail(email:) : .splash` — the partner shape verbatim. The forced-sign-out stream resets `route = .login` (the partner `.task` over `forcedSignOutStream` + the Android `SessionViewModel.events` / `ForcedSignOut` → SignIn-clearing-the-backstack, `CleansiaNavHost.kt:104-115`).
- **`NavigationStack` is the intra-audience push container** (the ADR-0020 D4 split) — within `.home`, the tab content (T-0314) and the booking sheet (T-0313) push/present over a stack; the top-level audience stays the enum switch. **CRC:** new `ios-customer-root-router` role (below) — the customer sibling of `ios-partner-root-router`, **does NOT know** any registration predicate (there is none).
- **Reviewer angle (the ADR-0020 #23 check, customer variant):** the audience is a closed `enum` switch, not a pushed path; seeded `.splash`; `afterLogin` returns `.splash` for verified / `.verifyEmail(email:)` for `requiresEmailConfirmation`; there is **no** login→`.home` path bypassing `.splash` and the empty-token gate; forced-sign-out replaces (not stacks) to `.login`. **A `.registrationLock`/`.onboarding` hard gate appearing in the customer router is a finding** (it would diverge from the Android customer gate — the customer onboarding is a soft in-shell nudge, T-0314).

#### Decision 2 — The auth surface + the social buttons: the T-0305 chain + the event-driven `AuthViewModel`; the official `ASAuthorizationAppleIDButton` ABOVE a Google button, both below the `OR` divider (APPLIES T-0305 + ADR-0018 Gate-DP + ADR-0016 AR-ACCT-2; NO new ADR)

**RULING (CONFIRMED): the customer auth screens (SignIn / SignUp / EmailVerify / ForgotPassword) mirror the partner T-0305 chain and the Android customer `AuthViewModel` event contract; the SignIn + SignUp screens carry, below the `OR` divider, the official `ASAuthorizationAppleIDButton` (SIWA) and a Google button. Per Apple HIG + App Store 4.8 (AR-ACCT-2), SIWA is presented EQUIVALENTLY to Google — same prominence, same surfaces.** The screens are native SwiftUI (Gate-DP), each citing its Android Compose counterpart. **A record, not a new ADR.**

- **The screen chain (parity with T-0305 + the Android customer screens):** SignIn (`SignInScreen.kt` — mascot → title → email/password → remember+forgot → primary Login → `OR` divider → social → "Register" footer), SignUp (`SignUpScreen.kt` — reusing the Core `PasswordPolicy`/`PasswordRuleList` from §7.5 D4, which was harvested **expressly** for this second customer call site), EmailVerify (`EmailVerifyScreen.kt` — the §7.5 D2/D3 pattern: the email rides the `.verifyEmail(email:)` associated value, the confirm-code reuses the shared empty-token gate, the resend uses the threaded email), ForgotPassword (`ForgotPasswordScreen.kt`). All four ride the **existing hand-written Core auth spine** (`AuthApiClient` — login/register/confirmEmail/resendConfirmation/forgotPassword are already there; **the customer "Register" uses `api/Auth/Register`** — the customer host's self-registration path, NOT `RegisterEmployee` — see Decision 3) — **no new anon allow-list entry for the email paths** (`AnonymousAllowList.customer` already carries `sharedAuth`).
- **The event-driven VM (the load-bearing Android-parity shape).** Mirror the Android customer **single `AuthViewModel`** that serves all four screens and emits an **`AuthOutcome`** the router consumes — it **never navigates directly** (`AuthViewModel.kt:217-226` + the `CleansiaNavHost` `LaunchedEffect(state.outcome)` consumers `:160-281`). The iOS form: a `CustomerAuthViewModel` (or per-screen VMs sharing one outcome subject — the dev picks, both honor the contract) publishes a `LoginSuccess`-style outcome via the partner `PassthroughSubject` pattern (`LoginViewModel.swift:23,34`), and `CustomerRootView` maps it (`SignedIn → .home`, `NeedsEmailConfirm(email) → .verifyEmail(email:)`, `PasswordReset → .login`). The **outcome carries the email** for the verify branch (the §7.5 D2 associated value). The Google/Apple paths feed the **same** outcome surface — a `SignedIn` (or `NeedsEmailConfirm` if the social account's email is unverified) — so the router has **one** outcome contract regardless of provider (mirrors `signInWithGoogle` folding into `toAuthUiState`, `AuthViewModel.kt:147-153`).
- **The social-button placement (the HIG/4.8 ruling).** Below the `OR` divider (`LabelledDivider`, the Android `SignInScreen.kt:147` parity), render **two** buttons, **SIWA first (top), Google second** — the conventional iOS ordering (Apple's own button is the platform-native primary on iOS). **SIWA uses the official `ASAuthorizationAppleIDButton`** (a `UIViewRepresentable` wrapper if needed — Apple requires *its* button for SIWA, you may not draw your own; this is a Gate-DP "iOS-wins / use the native control" case, Decision 6). **Google uses a styled `CleansiaOutlinedButton`** with the Google glyph (the Android uses `CleansiaOutlinedButton` with a placeholder mail icon, `SignInScreen.kt:152-157` — iOS ships the real Google "G"; Google's brand guidelines permit a custom button). **AR-ACCT-2 (App Store 4.8):** because the app offers Google (a third-party social login), it **must** offer SIWA equivalently — same screens (SignIn + SignUp), same prominence, no dark-patterning Google over Apple. Both surfaces (SignIn, SignUp) carry both buttons (the Android wires Google on both, `CleansiaNavHost.kt:182,220`). **CRC:** new `ios-customer-auth-vm` + `ios-social-sign-in-controllers` roles (below).
- **The nonce flow + provider wiring (per §7.14 D6).** Apple: generate a cryptographically-random **raw** nonce, set `request.nonce = SHA256(rawNonce)` on the `ASAuthorizationAppleIDRequest`, request `.fullName` + `.email` scopes, and on success POST the **`identityToken` + the RAW nonce** (+ first/last name **only on first authorization** — Apple returns them once) to `POST /api/Auth/AppleAuth`. Google: GoogleSignIn-iOS (SPM) with `serverClientID` = the backend `Google:ClientId` (§7.14 D5) → `result.user.idToken.tokenString` → `POST /api/Auth/GoogleAuth`. Both return `JwtTokenResponse` in the JSON body (native clients, no cookie) → the **shared empty-token gate** (`resolveEmailGate`) → the **same Keychain `TokenStore` single mutation path** (`persist`). The provider-acquisition controllers **return a typed result and never navigate** (the `GoogleSignInController` discipline, `GoogleSignInController.kt:38-87`) — the VM maps the result to an `AuthOutcome`.

#### Decision 3 — Where the provider-acquisition code lives: the app-local `CleansiaCustomer` target (provider acquisition is customer-only) riding the Core spine; the social network calls are TWO new methods on the SAME hand-written `AuthApiClient` (APPLIES ADR-0013 D1/D3 + ADR-0019 + §7.14 D6; NO new ADR)

**RULING (CONFIRMED): the provider-ACQUISITION code (the `ASAuthorization` controller + the GoogleSignIn-iOS controller + the nonce generation) lives APP-LOCAL in the `CleansiaCustomer` target — it is customer-only and pulls a customer-only SPM dep + the SIWA entitlement. The provider-CONSUMPTION (the `GoogleAuth`/`AppleAuth` POSTs + the empty-token gate + the Keychain persist) is TWO new methods on the EXISTING hand-written Core `AuthApiClient` spine — the same single-mutation Keychain path login/confirm already use.** This splits along the **ADR-0013 D3 line** (shared auth spine in Core; app-specific composition + system-framework acquisition in the app target) and rides the **ADR-0019** spine for everything network. **A record, not a new ADR.**

- **Acquisition = app-local (the `CleansiaCustomer` target).** The `ASAuthorizationController` delegate flow, the raw-nonce + SHA256 generation, and the GoogleSignIn-iOS `signIn(withPresenting:)` call are **customer-app-only** — partner does **not** offer social login (verified: the partner `LoginView.swift` has no social buttons), so this code must **not** go in Core (it would force the GoogleSignIn-iOS SPM dep + the SIWA entitlement onto the partner target, an ADR-0013 D3 violation). Each provider is a small controller mirroring the Android `GoogleSignInController` (returns a typed `SocialSignInResult{success(idToken, rawNonce?, email, first, last) | cancelled | noAccount | notConfigured | failure}`, **never navigates**, swallows-and-logs the cancel/no-account cases — `GoogleSignInController.kt:74-86`). These ride a **Core protocol** (`SocialSignInProviding`, fakeable) so the VM unit-tests against fakes (Decision 5).
- **Consumption = the Core spine (two new `AuthApiClient` methods).** Add `googleAuth(...)` + `appleAuth(...)` to the hand-written `AuthApiClient` (`Auth.swift`), each: POST the command to `api/Auth/GoogleAuth` / `api/Auth/AppleAuth` via the existing `post(...)` (anon session, no Bearer), decode `JwtTokenResponseDto`, and **route the result through the SAME `resolveEmailGate`** (`Auth.swift:175-188`) → `LoginOutcome` (`authenticated` | `unverifiedEmail`) → the SAME `persist` single Keychain mutation (`:292-301`). **This is the load-bearing seam reuse:** the §7.14 D6 mandate "iOS mirrors the Android response contract (`isEmailConfirmed==false || empty token => email-verify, not error`), then persists into the same Keychain `TokenStore` single mutation path" is satisfied for free because the gate + persist are **already** factored — the social methods are ~10 lines each that reuse the existing machinery. **A parallel social-specific token-write path is a finding** (it would fork the single-mutation invariant the spine guarantees). These live on the **Core** spine (not app-local) because the spine + `TokenStore` are Core (ADR-0013 D1) and the empty-token gate is the security-load-bearing contract that must be **one** implementation; only the *acquisition* is app-local.
- **The `CustomerAuthClient` binding (the generated-client side).** Where the social commands' **typed request DTOs** are needed (`GoogleAuthCommand` exists in `CleansiaCustomerApi`; `AppleAuthCommand` lands on regen — Decision 5), the request bodies are hand-written `Encodable` structs on the spine (the partner pattern — `LoginRequest`/`RegisterEmployeeRequest` etc. are hand-written in `Auth.swift`, NOT the generated client; the social commands follow suit so they ride the anon `noAuthSession` path with no Bearer). The generated `CleansiaCustomerApi` is the binding for the **rest** of the customer surface (orders/catalog/etc., T-0314) on the ADR-0019 `RequestBuilderFactory` — **not** for the hand-written anon auth paths (the header-parity-contract §3 "anon auth paths are hand-written, excluded from codegen" rule, confirmed by `Auth.swift`).
- **The allow-list addition.** `/api/auth/appleauth` must be added to `AnonymousAllowList.sharedAuth` (it is **absent** today — verified `AnonymousAllowList.swift:15-26`; `googleauth` is present `:19`). This is one line, lands **with** the social methods, and is covered by the TC-IOS-ANON sibling (Decision 7). The customer host already anon-allows the guest-booking paths (`:28-39`), unchanged.
- **CRC:** `ios-social-sign-in-controllers` (app-local, ASAuthorization + GoogleSignIn-iOS, returns typed results, never navigates) + an extension of the `ios-generated-client-auth-bridge`/spine role note (the spine gains two social methods reusing the existing gate + persist). **Does NOT know** (the acquisition controllers): the Keychain, the empty-token gate, navigation, or how the JWT is persisted — they hand the idToken (+ rawNonce) to the VM and stop.

#### Decision 4 — Scope vs T-0313/T-0314: T-0312 = shell SCAFFOLD (placeholder tabs) + FULL auth (incl. Google + Apple); the Book FAB is PRESENT but INERT (the wizard is T-0313); tab CONTENT is T-0314 (APPLIES the T-0304 shell-scaffold precedent + the T-0313/T-0314 dependency rows; NO new ADR)

**RULING (CONFIRMED): T-0312 ships the shell SCAFFOLD + the FULL auth surface. The four tabs are PLACEHOLDERS (exactly as the T-0304 partner shell shipped 3 placeholder tabs). The center Book FAB is PRESENT and visible but its ACTION is INERT/deferred — the booking wizard it opens is T-0313 (the §7.2/§7.4 inert-affordance precedent). Home/Orders/Rewards/Profile CONTENT is T-0314.** No new trade-off (the T-0304 precedent + the T-0313/T-0314 `depends_on` rows own the call) — a **scope record**.

**IN — T-0312 acceptance scope:**
- **The customer root + splash** — `CustomerRootView` (Decision 1) + the thin session-presence `SplashGateViewModel` (no status round-trip) + the splash screen (the Android `SplashScreen.kt` mascot/wordmark parity, Gate-DP).
- **The shell scaffold** — `CustomerShellView`: a native SwiftUI `TabView` with the **4 tabs in the Android `MainTab` order** (Home·Orders·Rewards·Profile, `MainShell.kt:66`) + the **center Book FAB**. The four tabs are **placeholder content** (their real content is T-0314). Gate-DP applies (cite `MainShell.kt`; the Android floating-pill `CustomBottomBar` + overlapping `BookFab` → a native iOS form — Decision 6).
- **The FULL auth surface** — SignIn / SignUp / EmailVerify / ForgotPassword (Decision 2) + the event-driven VM + **both** social providers wired end-to-end (Decision 3), code + unit-tested against fakes/the mockable spine (Decision 5).
- **The auth → shell handoff** — a successful auth (email, Google, or Apple) → `.home` (the shell scaffold); logout (from the placeholder Profile tab's logout affordance, or the forced-sign-out stream) → `.login`.

**DEFERRED — explicitly out of T-0312, with the ticket each lands in (the deferral map):**
- **The Book FAB's ACTION (the booking wizard)** → **T-0313** (the 3-step Bolt-style anchored sheet + Stripe). T-0312 renders the FAB and wires `onBookClick` to an **INERT closure** (present + visible, opens nothing yet — the §7.2/§7.4 inert-affordance precedent; the Android FAB calls `openBooking` which opens the `BookingBottomSheet`, `MainShell.kt:242,311` — T-0312 stubs the destination).
- **Home / Orders / Rewards / Profile tab CONTENT** → **T-0314** (the customer parity tail). T-0312's tabs are shared placeholders (the T-0304 "3 other tabs may be minimal/placeholder" precedent). The Profile tab carries a **minimal logout affordance** so the auth↔shell loop is testable end-to-end (the partner T-0304 shell had a working sign-out before the Profile tab content shipped in T-0310).
- **The soft in-shell onboarding nudge** (the "missing phone → onboarding" `LaunchedEffect`, `MainShell.kt:156-181`) → **T-0314** (it needs the EditProfile/onboarding screens). T-0312's `.home` lands unconditionally for an authed session (Decision 1).
- **LIVE social sign-in** → **OWNER-GATED** (the §7.14 ship-vs-owner-gated split): the code + the entitlement + the SPM dep ship in T-0312; live Google/Apple sign-in waits on the owner provisioning (T-0344 Apple capability + `Apple:BundleId`; T-0345 Google client ids — §7.14 MANUAL_STEPs). Reviewer/Gate-SEC verify the **seam + fail-closed behavior**, not a live sign-in (the T-0311-gated-by-T-0342 pattern).

**Why this is right (not under-scoping):** T-0312's job is the **customer foundation** — the root/router + the shell scaffold + the *complete* auth front door (the hardest, most security-sensitive customer surface, and the one the App-Review 4.8 obligation lands on). Auth is shipped **whole** (not sliced thin) because a half-auth surface is not shippable and the social/4.8 piece is the gating compliance work. The tabs + the FAB action are **additive parity** that land when their screens exist (T-0313 wizard, T-0314 tabs) — reached by **filling** the scaffold, not re-deciding the root/shell/auth seams. Rendering the FAB inert + the tabs as placeholders (the T-0304 precedent) keeps the shell visually at parity while honestly deferring the destinations that don't exist yet.

#### Decision 5 — The generated-client dependency: build the shell + root + provider-acquisition + the auth VMs AHEAD of the regen (against a Core `SocialSignInProviding` protocol + the spine's own hand-written request DTOs + fakes); bind the concrete `AppleAuthCommand` + the live `appleAuth` POST AFTER the regen (APPLIES the T-0305/T-0309 build-against-protocol-then-bind precedent; NO new ADR)

**RULING (CONFIRMED): the regen of `customer-mobile-api.json` → `CleansiaCustomerApi` (incl. the new `AppleAuthCommand`) is a MANUAL_STEP (owner; §7.14). MOST of T-0312 is built AHEAD of it; only the concrete AppleAuth binding + the live Apple POST need the regen.** This applies the precedent that VMs build against a protocol with fakes and the generated client binds last (T-0305 auth, T-0309 payroll). **A record, not a new ADR.**

- **Buildable AHEAD of the regen (no generated-client dependency):**
  - **The whole shell + root** — `CustomerRootView`, `CustomerShellView` (4 tabs + Book FAB), the `SplashGateView`/`SplashGateViewModel` — they touch only the Core spine's `hasValidSession` + the router, no generated DTOs.
  - **The email auth chain** — SignIn / SignUp / EmailVerify / ForgotPassword + the VMs — they ride the **hand-written** Core spine methods (login/register/confirmEmail/resend/forgot), which use **hand-written** request DTOs (not the generated client). Buildable now.
  - **The provider-ACQUISITION wiring** — the `ASAuthorization` nonce flow + the GoogleSignIn-iOS controller behind the `SocialSignInProviding` Core protocol — pure system-framework/SPM code, no generated DTOs. Buildable now (once the SPM dep + entitlement are added — Decision 6).
  - **The `googleAuth` spine method + its hand-written request DTO** — `GoogleAuthCommand` **exists** in `CleansiaCustomerApi` today, but per Decision 3 the spine uses a **hand-written** `GoogleAuthRequest` (the `LoginRequest` pattern) so it rides the anon `noAuthSession` — so even `googleAuth` doesn't depend on the regen. Buildable now.
  - **The auth VMs against fakes** — the `CustomerAuthViewModel` unit-tests against a fake `SocialSignInProviding` + a fake/mockable `AuthSpine` (the partner `LoginViewModelTests` pattern, which injects a fake `LoginClient`). The full outcome FSM (SignedIn / NeedsEmailConfirm / empty-token-gate / cancel / failure) is tested **without** a live client or the regen.
- **Needs the regen (the small tail):**
  - **The concrete `appleAuth` spine method's request DTO** — per Decision 3 this is also a **hand-written** `AppleAuthRequest(identityToken, rawNonce, firstName?, lastName?)` (mirroring `GoogleAuthRequest`), so strictly it does **not** need the generated `AppleAuthCommand` either — **BUT** the **backend `AppleAuth` endpoint must exist** (T-0343) for the live POST to have a target, and the regen confirms the contract shape (field names/casing) the hand-written DTO must match. So: **the `appleAuth` method can be written now against the §7.14 D1 contract** (`IdentityToken, RawNonce, FirstName, LastName`), and the **live POST is validated** once T-0343 ships + the regen confirms the wire shape. This is the §7.14 ship-vs-gated split: the code merges against the documented contract + a stubbed/fake verifier; the live round-trip is owner/backend-gated.
- **The MANUAL_STEP entry (owner):** regen `customer-mobile-api.json` + `CleansiaCustomerApi` after the backend `AppleAuth` endpoint + DTOs land (T-0343) — the §7.14 MANUAL_STEP #2. T-0312 flags it; the bulk of T-0312 does **not** wait on it. **Practically: T-0312 can land its three slices against the documented contract; the live Apple POST is the only thing whose end-to-end acceptance waits on T-0343 + the regen + the owner Apple provisioning.**

#### Decision 6 — Gate-DP divergences + the new SPM dep + the SIWA entitlement (APPLIES ADR-0018 + ADR-0016 + the §7.14 MANUAL_STEPs; NO new ADR)

**RULING: the Compose→SwiftUI translations below are sanctioned Gate-DP component swaps (layout/flow/branding unchanged, native components, iOS-wins-on-conflict, noted in-ticket); GoogleSignIn-iOS is a new customer-only SPM dep; the SIWA entitlement (`com.apple.developer.applesignin`) lands in the customer `project.yml`.**

- **The shell TabView + center FAB (the biggest divergence).** Android's customer shell is a **`HorizontalPager` + a custom floating-island pill `CustomBottomBar` with a center `BookFab` half-overlapping the pill** (`MainShell.kt:248-474`) — a Wolt/Bolt-style custom nav. iOS: a **native `TabView`** for the 4 tabs (Home·Orders·Rewards·Profile — the §7.4/T-0304 partner precedent: the Android floating pill → native `TabView` is the sanctioned ADR-0018 D3 swap) **+ a center Book FAB**. The FAB is the design signature, so it is **overlaid** above the `TabView`'s tab bar (a `ZStack`/`.overlay` with the circular primary-tinted button, the `BookFab.kt:452-474` styling at parity) rather than as a 5th tab — native `TabView` has no center-action slot, so the overlay is the iOS-native way to honor the Android center-FAB layout. **Cite `MainShell.kt`; note the pager→TabView + the FAB-as-overlay divergence in-ticket** (AR-DP-3). The FAB ACTION is inert in T-0312 (Decision 4).
- **The Apple/Google buttons.** SIWA = the **official `ASAuthorizationAppleIDButton`** (a `UIViewRepresentable` — Apple mandates its own control; you may **not** draw a custom SIWA button — this is a Gate-DP "use the native control" requirement, AR-ACCT-2). Google = a `CleansiaOutlinedButton` with the real Google glyph (the Android placeholder mail icon → the real "G", `SignInScreen.kt:152-157`). **The `OR` divider + the social block placement match the Android layout** (below the primary button — AR-DP-1).
- **Material → native** — `CleansiaTextField`/`CleansiaPrimaryButton`/`CleansiaOutlinedButton`/`CleansiaCheckbox`/`LabelledDivider`/`CleansiaTextLink` are **already** in the iOS Core component set (verified used in the partner `LoginView`) — the customer auth screens reuse them (no new components needed beyond the SIWA button wrapper + the Google-glyph button).
- **The new SPM dep:** GoogleSignIn-iOS, added to the **`CleansiaCustomer` target only** (Decision 3 — not Core, not partner). Pinned version, recorded in the customer `project.yml`. (Apple's `AuthenticationServices` is a first-party framework — no SPM dep.)
- **The SIWA entitlement:** `com.apple.developer.applesignin` in the customer target's entitlements + `project.yml` (the T-0308 NSCamera-plist-in-`project.yml` precedent for in-ticket capability declaration). **Ships in T-0312** (declared); the **owner enables the Sign in with Apple capability on the `cz.cleansia.customer` App ID + sets `Apple:BundleId`** (§7.14 MANUAL_STEP #3 / T-0344) before live sign-in. Google's **reversed-client-id URL scheme** in the customer Info.plist is added when the owner provisions the iOS Google client id (§7.14 MANUAL_STEP #4 / T-0345) — T-0312 declares the slot; the live value is owner-gated. **Gate-AR (§10.3) runs in-ticket:** the SIWA entitlement is a capability the ticket introduces, so it carries its declaration + the privacy-manifest review in-ticket (no orphan capability — the entitlement ships **with** the SIWA button that uses it).

#### Decision 7 — The slice plan (T-0312 is M → 3 slices) + the security-touching flag + the TC-IOS-* test ids

**RULING: 3 slices. Slice A is the scaffold (no auth network); Slices B + C are the auth surface and are SECURITY-TOUCHING → Gate-SEC runs in PARALLEL on B + C.** (This record fixes the architectural seams; the security enforcement — the empty-token gate + the social-token handling + the single Keychain mutation + the anon allow-list — is the security charter's Gate-SEC, out of this record's scope.)

- **Slice A — shell + root + `CustomerRootView` + splash + the shell scaffold.** The `CustomerRootView` flat-enum router (Decision 1), the thin `SplashGateView`/VM, the `CustomerShellView` (4-tab `TabView` + the center Book FAB rendered + inert), the splash screen, the placeholder tabs + the minimal Profile-logout affordance. **No auth network code** (it lands on the existing spine in B). Gate-DP. **Tests:** `TC-IOS-CUSTOMER-ROUTER-SEED` (seeds `.splash`), `TC-IOS-CUSTOMER-SPLASH-RESOLVE` (valid session → `.home`; no/expired → `.login`; **no status round-trip**), `TC-IOS-CUSTOMER-SHELL` (4 tabs in `MainTab` order + the FAB present; FAB action inert).
- **Slice B — auth core (SignIn / SignUp / EmailVerify / ForgotPassword + the event-driven VM), email paths only.** The four screens (native SwiftUI, Gate-DP, reusing Core `PasswordPolicy`/`PasswordRuleList`), the `CustomerAuthViewModel` event contract (`SignedIn`/`NeedsEmailConfirm(email)`/`PasswordReset` → the router), the `.verifyEmail(email:)` associated-value threading, the shared empty-token gate reuse, the customer `api/Auth/Register` path. **SECURITY-TOUCHING (Gate-SEC):** the empty-token gate (`200+empty/blank Token` or `isEmailConfirmed==false` → `.verifyEmail`, never `.home`), the anon-path Bearer discipline (the §7.5 D3 double-skip), the single Keychain mutation. **Tests:** `TC-IOS-CUSTOMER-AUTH-OUTCOME` (each outcome → the right route; verify branch carries the email), `TC-IOS-CUSTOMER-EMPTYTOKEN` (the customer sibling of TC-IOS-EMPTYTOKEN — empty/blank token + unconfirmed → no `.home`), `TC-IOS-CUSTOMER-VERIFY-EMAIL-ARG` (the email rides the associated value; cold-start-nil-email degrades to disable-resend, the §7.5 D2 guard).
- **Slice C — social (Google + Apple).** The app-local `SocialSignInProviding` controllers (ASAuthorization nonce flow + GoogleSignIn-iOS), the two new Core-spine `googleAuth`/`appleAuth` methods (reusing the gate + persist), the official SIWA + Google buttons on SignIn + SignUp, the `appleauth` allow-list addition, the SPM dep + the SIWA entitlement + the URL-scheme slot. **SECURITY-TOUCHING (Gate-SEC):** the social-token → empty-token-gate → single-Keychain-mutation path (no parallel social write path), the `appleauth` allow-list completeness, the nonce flow correctness (raw nonce to the backend, SHA256 to Apple), no idToken/identityToken logged. **Tests:** `TC-IOS-SOCIAL-OUTCOME` (Google/Apple success → `SignedIn`/`NeedsEmailConfirm` via the SAME gate; cancel → silent; notConfigured/failure → the right snackbar — the `GoogleSignInResult` branch parity), `TC-IOS-SOCIAL-NONCE` (raw nonce generated, `request.nonce == SHA256(raw)`, the RAW nonce is what's POSTed), `TC-IOS-CUSTOMER-ANON` (the customer sibling of TC-IOS-ANON — `/api/auth/appleauth` + `/api/auth/googleauth` are anon; a stored Bearer is **not** attached on the social paths). **Live sign-in is owner-gated** (Decision 4 / §7.14) — these tests assert the seam + fail-closed mapping against fakes, **not** a live provider round-trip.

**Slice ordering rationale:** A is independent (the scaffold, no auth network) and can be reviewed/merged first — it de-risks the root/shell shape. B + C both touch the auth spine and carry Gate-SEC; B (email) before C (social) so the shared gate + the VM outcome contract are proven before the social paths fold into them. All three are **M-sized** in aggregate; none is `L`.

#### New CRC roles (added with the T-0312 wiring)

- **`ios-customer-root-router`** — `CustomerRootView` + its `Route` enum (the customer sibling of `ios-partner-root-router`): *responsibility:* select the customer app's **top-level audience** (splash / login / register / forgotPassword / verifyEmail / home-shell) and replace the root on an audience change. *Collaborators:* `SessionManager` (the `forcedSignOutStream` reset + `hasValidSession` seed), the thin `SplashGateViewModel` (session-presence resolve), the auth VM's outcome subject. *Does NOT know:* **any registration/eligibility predicate (the customer has NONE — if this router ever evaluates profile/documents/contract or hard-gates on profile-completeness, the responsibility is wrong: the customer onboarding is a SOFT in-shell nudge, T-0314)**, how any screen renders, or any business payload.
- **`ios-customer-auth-vm`** — `CustomerAuthViewModel` (the Android `AuthViewModel.kt` event-driven parity): *responsibility:* drive the customer auth FSM for all four screens (+ the two social providers) and **emit an `AuthOutcome` the router consumes — NEVER navigate directly**. *Collaborators:* the Core auth spine (`AuthApiClient` — email + the two social methods), the `SocialSignInProviding` controllers, the snackbar, the `AppSettingsStore` (the email-locale language tag). *Does NOT know:* the navigation graph (it emits outcomes; the router maps them), the Keychain/token persistence (the spine owns it), how the social idToken is acquired (the controllers own it).
- **`ios-social-sign-in-controllers`** — the app-local `SocialSignInProviding` impls (the `ASAuthorization` controller + the GoogleSignIn-iOS controller, in the `CleansiaCustomer` target): *responsibility:* acquire a provider idToken (+ for Apple the raw nonce + first-auth name/email) and **return a typed result — NEVER navigate, NEVER touch the token store**. *Collaborators:* `AuthenticationServices` / GoogleSignIn-iOS, the customer config (the Google `serverClientID`, the Apple bundle id). *Does NOT know:* the Keychain `TokenStore`, the empty-token gate, the network POST (the spine owns it), or navigation. **If a controller writes the JWT or navigates, the responsibility is wrong — it hands the idToken to the VM and stops.**

(The existing Core spine role — `ios-generated-client-auth-bridge`/the `AuthApiClient` spine — gains two social methods that **reuse** the existing `resolveEmailGate` + `persist`; it is **not** a new role and the single-Keychain-mutation invariant is unchanged.)

#### Open question for the owner (non-blocking)

- **None that blocks T-0312's code.** The two LIVE-sign-in provisioning gates are **already filed** as §7.14 owner MANUAL_STEPs / tickets — **T-0343** (backend `AppleAuth` + the regen), **T-0344** (Apple capability + `Apple:BundleId`), **T-0345** (Google iOS + web client ids / IMP-1). T-0312 ships code-complete against the documented contract + the entitlement/SPM dep; live Google/Apple sign-in is the owner's acceptance once those land (the T-0311-gated-by-T-0342 pattern). **One thing to surface, not block on:** confirm the customer "Register" uses `api/Auth/Register` (the customer self-registration path) rather than `api/Auth/RegisterEmployee` (the partner path the Core spine currently hard-codes, `Auth.swift:137`) — the spine's `register(...)` is partner-shaped; the customer wave needs the customer endpoint (verified the customer host exposes `/api/Auth/Register` via the `RegisterCommand` in `CleansiaCustomerApi`). This is a one-method spine addition (a `customerRegister` / parameterized path), recorded so Slice B handles it; it is an implementation detail, not an owner decision.
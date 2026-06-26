# Sprint 12 — iOS PORT (Wave 10): parity Swift/SwiftUI customer + partner apps

**Status:** PHASE 0 FOUNDATION DONE + MAC-VERIFIED + MERGED (2026-06-26) · **PHASE 1 (T-0303) DONE** — proving vertical green on `phase/ios-phase1` · **PHASE 2 STARTED — T-0304 (partner shell + RegistrationLock + SplashGate) DONE** on `phase/ios-phase2` (3 commits; reviewer + security/Gate-DP ALL APPROVE) · Phase 2+ tail proposed
**Created:** 2026-06-23
**Updated:** 2026-06-26
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
| **T-0305** | Partner auth completeness — Register/Forgot/ConfirmEmail/Onboarding chain | M | **proposed** | ios | T-0303 | — | 2 (partner) |
| **T-0306** | **Map seam + MapKit default** — `MapProvider`/`GeocodingService` protocol in `CleansiaCore` + `MapKitMapProvider` + the partner `AddressPicker` (first map surface). **iOS-16 variant (ADR-0014 D6′):** `Map(coordinateRegion:annotationItems:)` for the picker; `MKMapView` via `UIViewRepresentable` for the full-bleed map + polygon overlays — NO iOS-17-only `Map {...}`/`Marker`/`MapPolygon` | M | **proposed** | ios | T-0300 | — | 2 (**HARD AREA #2 — first half**) |
| **T-0307** | **Partner order work-loop** — OrdersList + OrderDetail (full-bleed map + 3-snap sheet) + the **OnTheWay** lifecycle (Take→NotifyOnTheWay→Start→Complete) + checklist/notes/issues/timeline | **L → split** | **proposed** | ios | T-0304, T-0306 | — | 2 (**HARD AREA #3**) |
| **T-0308** | **Partner photo upload** — camera capture → **JSON base64** photos (partner shape) on OrderDetail | M | **proposed** | ios | T-0307 | — | 2 (HARD AREA #3 cont.) |
| **T-0309** | Partner earnings + invoices + PeriodPay (`EmployeePayroll/GetPeriodPays` — a regen'd-spec endpoint) | M | **proposed** | ios | T-0304 | — | 2 (partner) |
| **T-0310** | Partner profile section editors + onboarding-chain + settings + **Devices** (Device/Mine list + revoke) + Notifications | M | **proposed** | ios | T-0304, T-0306 | — | 2 (partner) |
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
- **T-0307 (partner order-loop) →** (a) OrdersList; (b) OrderDetail map+3-snap sheet shell;
  (c) the OnTheWay→Start→Complete lifecycle + actions; (d) checklist/notes/issues/timeline.
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
- **Mechanical:** the Xcode workspace builds; `CleansiaCore` + both app targets compile; the codegen step
  produces the client from the on-disk spec (no hand-edit); the Swift test suites run. **✅ T-0303 evidence:**
  `swiftformat --lint` + `swiftlint --strict` clean; **CleansiaCore 93 + CleansiaPartner 17** tests pass on
  the iPhone 17 simulator; the T-0302 first real generation (`8d4cfe3`) produced the client from the
  regenerated on-disk spec (`9232335`), no hand-edit. **✅ T-0304 evidence (`55b39aa`+`c269360`+`df71181`):**
  `swiftformat --lint` + `swiftlint --strict` clean; **CleansiaCore 93 + CleansiaPartner 61** tests pass on
  the iPhone 17 simulator (the partner suite grew 17→61 with the gate/router/shell tests incl. TC-IOS-REGLOCK).

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

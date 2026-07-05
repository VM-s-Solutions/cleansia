---
id: T-0373
title: "iOS customer Home upsell carousel — port the Android HorizontalPager of gradient mascot upsell slides (TabView(.page) + BrandGradients)"
status: done
size: M
owner: ios
created: 2026-07-03
updated: 2026-07-03
depends_on: [T-0368, T-0372]
blocks: []
stories: []
adrs: [ADR-0014, ADR-0018]
layers: [ios]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: phase/ios-fix1 on-device shakeout diagnosis (2026-07-02, cluster brand-assets — design delta #1)
---

> **The largest single visual gap on the most-seen screen.** Android's HomeTab builds a `HorizontalPager` of
> gradient `UpsellSlide` cards (Plus upsell / setup-recurring / welcome / referral / book-hero), each with a
> dedicated mascot and per-slide gradient + CTA (`HomeTab.kt:426-500` — `buildList` of `UpsellSlide` with
> `mascotRes` + gradients + `rememberPagerState`). iOS `HomeTab` is a flat static stack — GreetingHeader,
> ProfileNudgeCard, a plain sparkles `BookCard`, MembershipManagementCard, RecurringEntryRow
> (`HomeTab.swift:36-71`, `:151-172`, `:162`). An AR-DP-1 (layout-region) miss. Not iOS-16-specific.

## Context
Deliberately split from T-0372 because it is **structural, not just assets**: it needs the mascot imagesets
(T-0372) AND mounts on the restructured shell/pager surface (T-0368) — building it against the pre-T-0368
HomeTab would mean immediate rework, hence `proposed` until both land.

## Acceptance criteria
- [x] **AC1 (pager)** — HomeTab's `BookCard` is replaced by a SwiftUI `TabView(.page)` pager (the ADR-0018
  D3-style mapping for HorizontalPager) of gradient upsell cards; `MembershipManagementCard` +
  `RecurringEntryRow` stay below, as on Android.
- [x] **AC2 (slide model parity)** — Same slide predicate logic (`isPlus` / `showSetupRecurring` /
  `hasAnyOrders`), same slide order, same gradients — a `BrandGradients` set defined in `CleansiaCore`
  mirroring the Android values — each slide with its dedicated mascot (`mascot_ready`/`mascot_idea`/
  `mascot_mopping`/`mascot_cleaning` ×2) trailing-aligned, plus the per-slide CTA wiring (Plus →
  SubscribePlus, book-hero → booking sheet, referral → Rewards, setup-recurring → recurring flow).
- [x] **AC3 (Gate-DP)** — Review cites `HomeTab.kt:426-500` as the reference; divergences (if any) recorded
  as one-line notes (5 recorded below — divergence (1) filed as the Android follow-up **T-0375**); the
  pager renders correctly on the iOS 16.4 simulator (T-0374 leg) with no clipping against the T-0368
  pill-bar inset contract.
- [x] **AC4 (non-regression)** — Customer suite green; swiftformat/swiftlint --strict clean.

## Out of scope
- The mascot assets/animator (T-0372) and the shell/pager restructure (T-0368) — hard dependencies.
- Any new upsell slide not present on Android (parity port only).

## Implementation notes
- Nested pagers caution: the Home tab root itself lives inside the T-0368 shell content pager — the slide
  carousel is a SECOND, inner `TabView(.page)`; verify the gesture coexistence on iOS 16 (inner pager should
  win horizontal drags within its frame; this is the same composition Android ships).
- Slide card = gradient RoundedRectangle + text/CTA column + trailing mascot Image; height fixed to the
  Android card ratio.

## Status log
- 2026-07-03 — filed `proposed` by pm from the phase/ios-fix1 diagnosis (brand-assets cluster, design delta
  #1). Held until T-0368 (shell) + T-0372 (imagesets) are done — building against the pre-restructure HomeTab
  would be immediate rework.
- 2026-07-03 — dev (Slice F): implemented on phase/ios-fix1, uncommitted per the batch rule. Reference:
  `HomeTab.kt:426-500` (buildList) + `:399-568` (SmartUpsellCarousel/UpsellSlideCard) + `MainShell.kt:265-280`
  (CTA wiring). **Slide model** ported as pure data (`UpsellSlide.slides(isPlus:hasAnyOrders:showSetupRecurring:)`)
  with a semantic `Action` enum the view maps onto the HomeTab callbacks; `BrandGradient` (blue/purple/cyan
  dynamic pairs + fixed plusHero Sky950→Slate900) added to `CleansiaCore/DesignSystem` with light/dark values
  hex-identical to `BrandGradients.kt:22-31` + `Color.kt`. Pager = inner `TabView(.page(indexDisplayMode:
  .never))` + the Android custom dot row (active dot 24×8 grows, primary/outlineVariant), 6s auto-rotate,
  180pt card, r22, 20pt gutter inside the slide (full-viewport snap, no peek — the Android comment), text
  column 72% width, mascot 110pt bottom-trailing (no clip: 110 < 140 content height).
  **Red→green:** Core `BrandGradientTests` written first — captured compile-red (`Cannot find type
  'BrandGradient'`) then green; customer `UpsellSlideTests` (12 tests) written before `UpsellSlide`/L10n
  accessors existed (compile-red by construction, the T-0372 precedent) then green. Core 271 green, Customer
  405 green on iPhone 17 (iOS 26.3.1) AND on the iPhone14-iOS16 sim (16.4) — the pager APIs are 16.0-safe.
  swiftformat 0.60.1 --lint + swiftlint --strict clean. 15 new `home_hero_*`/`home_upsell_*` keys ×5 locales
  (values = the Android `values{,-cs,-sk,-uk,-ru}/strings.xml` lines 116-132); orphaned `home_book_*` keys +
  accessors + the old `BookCard` removed.
- 2026-07-03 — **Gate-DP divergences (one-line each, vs `HomeTab.kt:426-500`):**
  (1) `home_hero_greeting` drops Android's hardcoded ", Michael" (`strings.xml:116` bakes the owner's first
  name into every user's greeting ×5 locales) — **Android finding raised, do not port**; iOS uses the
  name-less "Welcome back" set.
  (2) Auto-rotate pause: Android skips the 6s advance while `isScrollInProgress`; `TabView` exposes no drag
  state, so iOS restarts the 6s countdown on every page settle (same "give the user time" intent).
  (3) Dot row is the Android custom row below the card (stock overlaid `UIPageControl` rejected — Android's
  dots sit under the banner and use theme colors).
  (4) CTA chip arrow = SF `arrow.right` (codebase convention) for Material `AutoMirrored.ArrowForward`.
  (5) The setup-recurring slide is additionally gated on a WIRED `RecurringBookingRepository` (unwired
  default → hidden), so the permanent-`[]` templates stream of the not-yet-wired shell can't false-positive
  the slide for Plus users.
- 2026-07-03 — **PENDING SHELL WIRING (blocked on the Slice D file lock, NOT done):** `CustomerShellView.swift`
  is owned by Slice D this batch, so HomeTab's two new callbacks + the repo ship as DEFAULTED init params and
  the call site is untouched. Interim behavior: setup-recurring slide hidden (unwired repo) and its CTA falls
  back to `onManageRecurring`; **the referral slide CTA is inert**. After Slice D lands, add to the
  `HomeTab(...)` call in `CustomerShellView.pager`:
  `recurringRepository: container.recurringRepository,`
  `onOpenReferral: { model.select(.rewards) },`  // MainShell.kt:271 selectTab(Rewards)
  `onSetupRecurring: { model.path = NavigationPath([ShellRoute.recurringList, ShellRoute.createRecurring(orderId: nil)]) }`
  — the PRE-SEEDED path is load-bearing: the shell's `createRecurring` destination wires
  `onCreated: { model.pop() }`, so a naive single `path.append(.createRecurring)` from Home would pop to the
  tab root after creation instead of landing on the recurring list (Android's fixed Path B,
  `CleansiaNavHost.kt:591-602`); mirror the `membershipSuccess.onSetupRecurring` wiring.
- 2026-07-03 — the "PENDING SHELL WIRING" entry above is **RESOLVED IN `e69a0283` ITSELF**: the Slice-D
  file lock released before this slice committed, so the wiring shipped WITH the slice —
  `recurringRepository` passed at the `HomeTab(...)` call site, referral CTA → `model.select(.rewards)`,
  setup-recurring CTA → the PRE-SEEDED `NavigationPath([recurringList, createRecurring])` (pop-on-created
  semantics preserved). No inert-CTA interim ever landed on the branch. Post-wiring Customer 405/405.
- 2026-07-03 — D+F review fold (`bfb1ca7a`): **F-1 (floor catch):** `BrandGradientTests` was RED on the
  iOS 16.4 runtime (18 failures) — caught ONLY because the T-0374 floor leg ran the suite there; fixed
  STRUCTURALLY — the light/dark hex stops become `BrandGradient`'s single source of truth (`colors`
  derives from them) and the tests assert the stops: OS-independent value equality; Core 272/272 green on
  BOTH iPhone 17 and iOS 16.4. Also folded: HomeTab's recurring source + referral/setup-recurring
  callbacks made REQUIRED (an optional-defaulted callback is a silently inert CTA — the exact failure
  class this phase fixed); the Book FAB glyph follows the T-0372 nearest-meaning ruling
  (`bubbles.and.sparkles`).
- 2026-07-03 — Gate-DP divergence (1) FILED as **T-0375**: Android `home_hero_greeting` bakes ", Michael"
  into every user's greeting (`strings.xml:116` ×5 locales; consumed at `HomeTab.kt:489`) — fix name-less
  or a real placeholder; iOS deliberately shipped the name-less set.
- 2026-07-03 — **done** by pm at phase close. Final-tree gates: Core 272/272 (both runtimes), Customer
  406/406, lint clean tree-wide; the 16.4 render smoke via the floor leg. REMAINING acceptance: the
  owner's signed-in Home render + nested-pager gesture feel — flagged in the phase PR.
- 2026-07-03 — **fix-round 2 (owner device pass): the FULL Android Home ported** — this slice only
  ported the carousel; the rest of `HomeTab.kt` was never mirrored (PM may re-home this entry to a new
  ticket). Uncommitted on `phase/ios-fix1` per the batch rule. **Section-by-section parity
  (`HomeTab.kt` → iOS):**
  | Android source | iOS |
  |---|---|
  | `AddressTopBar` `:313-365` (pin + "Cleaning at"/selected▾ + bell; bar opens the address picker; `selected ?? default ?? first` `:111-113`; bell `onNotificationClick = {}` `:228`) | `HomeTab.swift` `AddressTopBar` → shell `.sheet` `AddressManagerView` (row-tap select added, Android's selected-row styling `AddressManagerScreen.kt:355-425`); `HomeSections.displayedAddress`; bell rendered INERT = Android parity |
  | `SmartUpsellCarousel` `:234-242` | untouched (this slice's original scope) |
  | `OrderAgainCard` / `TrustStrip` fallback `:249-256`, title `:971-978`, "MMM d" `:684-692` | `HomeSectionViews.swift` + `HomeSections.mostRecentCompleted/recentBookingTitle/orderAgainWhen` |
  | `RecurringSchedulesSection` `:262-268` (Plus && active, top 3; rows → manage) | `RecurringSchedulesSection` + `HomeSections.activeRecurring` |
  | `PopularPackagesSection` `:273-279` (top-3 non-blank-id packages; tap seeds the sheet `BookingBottomSheet.kt:390-399`) | `PopularPackagesSection` + `HomeSections.popularPackages` + `BookingPrefill.withPackage` |
  | `RecentBookingsSection` `:282-289` (sort `:177-181`, gate `:185`, status chip `:1021-1039`, date·price `:1042`) | `HomeSecondarySections.swift` + `HomeSections.recentForDisplay/showRecent/statusChipLabel` |
  | `MilestoneProgressCard` `:295-300`, `:1074-1135` | `MilestoneProgressCard` + `HomeSections.showMilestone` (tier labels via `L10n.Rewards.tierLabel`) |
  | `SeasonalCard` `:303` | `SeasonalCard` → booking sheet |
  | `HomeSkeleton` first-paint gate `:196-215` (orders+membership+packages, 1.5s ceiling, never-revert) | `HomeSkeleton` + `HomeTabViewModel.firstPaintReady` watcher + `runFirstPaintCeiling()` |
  | data effects `:108-172` (membership-if-null, catalog-if-empty, recurring-if-Plus) | `HomeTabViewModel.refresh*` `.task`s; VM mirrors order/loyalty/membership/address/recurring repos + the catalog |
  | `MainShell.kt:264-282` CTA wiring + `:197-199` address warm + hydration/rebook (`BookingBottomSheet.kt:270-282`, `:305-374`) | `CustomerShellView` + `CustomerShellView+Booking.swift` (`openBooking`/`bookPackage`/`rebookOrder` seed the session `BookingViewModel` via pure `BookingPrefill`); shell prefetch now warms `savedAddressRepository` |
  **Sections REMOVED from iOS Home (Android has none):** GreetingHeader, ProfileNudgeCard,
  MembershipManagementCard (stays on Profile), RecurringEntryRow; dead keys `home_greeting`,
  `home_profile_nudge_*`, `home_recent_orders_title`, `home_see_all` deleted (accessors + xcstrings).
  **Strings:** 20 new keys ×5 locales verbatim from Android `values*/strings.xml:102-176` + `:1020`
  (`%1$s`→`%1$@` transposed — iOS `String(format:)` prints garbage on `%s`); harvested as a
  patterns-mobile.md row note. **New pure logic + tests-first:** `HomeSections` (16 tests) +
  `BookingPrefill` (8 tests) + saved-address selection (4 repo + 1 VM tests). Customer suite 441/441
  (1 skipped) on iPhone 17; swiftformat 0.60.1 --lint + swiftlint --strict clean; 16.4 sim build +
  launch OK — signed-out sign-in screenshot `agents/backlog/attachments/T-0373-fix2-ios164-signin.png`
  (signed-in Home needs the owner's device).
- 2026-07-03 — **fix-round 2 Gate-DP divergences (one-line each):**
  (1) Bell: inert on BOTH platforms (Android wires `{}`) — NOT routed to NotificationPreferences; a
  feed is T-0336-class work.
  (2) `selectedId` persistence: Android DataStore → iOS UserDefaults key on `SavedAddressRepository`
  (same wiped-on-signout semantics via `clear()`; also cleared when the selected address is deleted).
  (3) Rebook `countryIsoCode`: iOS keeps the draft's current value (iOS `SavedAddress` carries no ISO;
  Android assigns the matched saved address's — empty for server-loaded rows; `savedAddressId` drives
  the submit either way).
  (4) Catalog cache: Android's shared `CatalogRepository` singleton → the session-lived
  `BookingViewModel` as Home's `catalogSource` (one cache for Home + sheet; no second fetch path).
  (5) Catalog-refresh errors: Android silences `ApiError.Network` only; iOS uses the codebase-wide
  `showApiError` for all failures (the existing iOS convention).
  (6) Fresh-open draft reset: Android resets the sheet per open (`BookingBottomSheet.kt:298-303`); iOS
  keeps the deliberate session-lived draft (T-0313, `BookingDraftSurvivalTests`) — hydration therefore
  only fills a BLANK street, same guard as Android's.
  (7) Android's unused `onViewAllServices` param (dead in `HomeTab.kt`'s body) not ported.
- 2026-07-03 — **fix-round 2 findings (not fixed here, for PM routing):**
  (a) 10 `membership_*` xcstrings keys still carry Android-style `%1$s` and render garbage through
  `String(format:)` (e.g. "Active until –9"): membership_active_until, membership_cancelled_until,
  membership_cta_disclosure_trial{,_year}, membership_hero_then_price{,_year},
  membership_plan_per_{month,year}, membership_renews_on, membership_switch_dialog_message.
  `recurring_bookings_day_at_time` was fixed in this round (my Home section renders it).
  (b) The working-tree customer + Core `Localizable.xcstrings` arrived re-serialized by an Xcode
  string-catalog sync (junk auto-extracted keys `"%@"`, `"•  %@"`, `extractionState: stale` markers)
  despite `SWIFT_EMIT_LOC_STRINGS: NO` — NOT reverted (shared-file lane rule); edits made in the
  current serialization.
- 2026-07-04 — **fix-round 3 (owner profile/onboarding findings; PM may re-home to a new ticket):**
  **(1) ProfileOnboardingScreen ported** (`ProfileOnboardingView.swift` ← `ProfileOnboardingScreen.kt`):
  mascot_waving 160pt hero, named greeting via a REAL `%1$@` placeholder (`onboarding_greeting_named` —
  Android's key is placeholder-correct; the T-0375 hardcoded-name defect is `home_hero_greeting`, not
  this), `CleansiaPhoneInput` with the country-code helper, optional tap-to-pick birth-date field +
  helper, Save-and-continue (disabled until phone non-blank, loading while submitting), Skip for now.
  **Android trigger mirrored exactly** (`MainShell.kt:142-181` + `CleansiaNavHost.kt:346-388`): once per
  shell entry the gate forces a FRESH `GetCurrentUser` round-trip (never trusts the cached snapshot —
  Android's stale-`isProfileComplete` bug note) and fires iff `!(phoneOk && nameOk && emailOk) &&
  !hasSeenOnboarding(userId)`. `ProfileViewModel.needsOnboarding()` owns the decision;
  `CustomerShellView.prefetch()` awaits it inside the parallel prefetch; the ROOT lands the state — new
  pre-shell flat-enum case `CustomerRootView.Route.profileOnboarding` (ADR-0020; NOT a shell-stack push).
  **Skip** → `markOnboardingSeen(userId:)` only → lands Home; **Save** → `completeOnboarding` (names
  untouched; language = the resolved app tag ∈ {en,cs,sk,uk,ru} — the Android device-locale clamp,
  `ProfileViewModel.kt:105-106`) → marks seen → lands Home; a FAILED save leaves the gate unseen (error
  snackbar, screen stays). The seen-flag is **PER-USER** — `AppSettingsStore.hasSeenOnboarding(userId:)`/
  `markOnboardingSeen(userId:)` added to Core (customer `AppSettingsRepository.kt:40-47` parity; the
  prior global flag would have leaked "seen" across accounts on one device; partner's pre-auth carousel
  keeps the global pair via protocol-extension defaults). Strings: 9 new `onboarding_*` keys ×5 verbatim
  from Android `values*/strings.xml:736-755` (+ pre-existing `onboarding_birthdate_placeholder`),
  `%1$s`→`%1$@` transposed.
  **(2) profileIncomplete redirect explained (recorded owner-directed divergence — Android lands Edit
  Profile with no explanation too, but its users passed onboarding first):** `ShellRoute.editProfile`
  now carries `showBookingHint:`; the booking sheet's `onCompleteProfile` routes
  `model.openEditProfile(showBookingHint: true)`; EditProfileView renders an info banner
  (`profile_edit_booking_hint` ×5, house-style translations) and error-marks the blank required fields
  (phone via new `profile_edit_phone_required` ×5; names via existing `auth_error_*_required`; marks
  clear as the user types). The direct Profile→Edit entry passes `false` — unchanged. Only
  CustomerShellView's edit-profile destination + onCompleteProfile wiring touched (bar/pager untouched;
  CustomerBottomBar not touched — Slice lane respected).
  **Gate-DP divergences (one-line):** (a) Skip/Save land Home by ROOT-case switch (shell re-creates and
  re-prefetches once) vs Android's pop-over-live-shell — same landing surface, nothing user-visible to
  lose at that point; (b) the phone field is the Core floating-label `CleansiaPhoneInput`, which has no
  inner "+420 000 000 000" placeholder affordance (Android bakes that placeholder as a code literal) —
  the country-code guidance rides the helper line both platforms show; (c) Material `DatePickerDialog` →
  `.sheet` graphical `DatePicker` (the established EditProfileView mapping).
  **Red→green** (compile-red by construction for the new VM/store surface, tests written first):
  ProfileViewModelTests 9 onboarding cases (gate ×5 — trigger-after-forced-refresh / complete-profile /
  seen-per-user / never-loads / cached-fallback-on-failed-refresh; skip ×2; save ×2 incl.
  failure-leaves-gate-unseen + resolved-language) + Core `AppSettingsStoreTests` per-user ×3 +
  `CustomerShellRoutingTests` hint-flag ×2 + round-trip + `CustomerRootRouteTests` pre-shell case.
  Customer **453/453**, Core **275/275**, Partner **371/371** on iPhone 17 (partner rides the T-0370
  fix-round 3 date-wire change); swiftformat 0.60.1 --lint + swiftlint --strict clean. **iOS 16.4
  build+launch smoke:** the sim carried a signed-in incomplete-profile session, so the launch itself
  exercised the gate end-to-end — splash → shell prefetch → forced profile refresh → the pre-shell
  onboarding case rendered with the real first name:
  `agents/backlog/attachments/T-0373-fix3-ios164-onboarding.png`. Uncommitted on phase/ios-fix1 per the
  batch rule.
- 2026-07-04 — **fix-round 3 findings (not fixed here, for PM routing):**
  (a) Android auto-reopens the booking sheet when the user returns from Edit Profile with the phone now
  filled (`MainShell.kt:123,186-192` `reopenBookingAfterProfile` keyed on the phone transition); iOS has
  NO reopen path — after the banner-guided save the user reopens booking manually and rebuilds nothing
  (the draft survives), but the reopen affordance itself is missing. Needs its own slice if wanted.
  (b) Android's booking pre-flight ALSO toasts `error_booking_profile_incomplete` before navigating
  (`BookingViewModel.kt:335`); the iOS `.profileIncomplete` outcome navigates silently — the new
  destination banner now carries the explanation (owner-directed), but the Android snackbar + its key
  remain unported on iOS.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
- 2026-07-03 — dev (harvest note): one row folded into `patterns-mobile.md` (D3 table): customer
  `BrandGradients.kt` → the Core `BrandGradient` token enum (dynamic pairs + fixed `.plusHero`,
  `linearGradient` top-leading→bottom-trailing = Compose's default), with the "models carry the semantic
  token so tests compare gradients by case" idiom.
- 2026-07-03 reviewer (D+F combined, concurrent): **CHANGES** — **F-1** (the gradient tests were red on
  the iOS 16.4 floor runtime while green on iPhone 17) + the required-CTA hardening; folded in
  `bfb1ca7a`. **Empirical correction ON the review (recorded):** the reviewer's suggested F-1 fix —
  `UITraitCollection.performAsCurrent` around the `UIColor(Color)` roundtrip — was EMPIRICALLY WRONG on
  iOS 16: `UIColor(Color)` flattens the dynamic provider REGARDLESS of current traits (trait
  preservation arrived in iOS 17), so NO roundtrip works on the floor OS. The landed fix asserts the hex
  stops (the new single source of truth) instead — OS-independent. The reviewer's DIAGNOSIS stands as
  the catch (and as the T-0374 leg's proof-of-value); the fix diverged with evidence. PM reconciled
  2026-07-03: fold verified, slice advanced to done.

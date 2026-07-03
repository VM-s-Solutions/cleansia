---
id: T-0373
title: "iOS customer Home upsell carousel — port the Android HorizontalPager of gradient mascot upsell slides (TabView(.page) + BrandGradients)"
status: proposed
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
- [ ] **AC1 (pager)** — HomeTab's `BookCard` is replaced by a SwiftUI `TabView(.page)` pager (the ADR-0018
  D3-style mapping for HorizontalPager) of gradient upsell cards; `MembershipManagementCard` +
  `RecurringEntryRow` stay below, as on Android.
- [ ] **AC2 (slide model parity)** — Same slide predicate logic (`isPlus` / `showSetupRecurring` /
  `hasAnyOrders`), same slide order, same gradients — a `BrandGradients` set defined in `CleansiaCore`
  mirroring the Android values — each slide with its dedicated mascot (`mascot_ready`/`mascot_idea`/
  `mascot_mopping`/`mascot_cleaning` ×2) trailing-aligned, plus the per-slide CTA wiring (Plus →
  SubscribePlus, book-hero → booking sheet, referral → Rewards, setup-recurring → recurring flow).
- [ ] **AC3 (Gate-DP)** — Review cites `HomeTab.kt:426-500` as the reference; divergences (if any) recorded
  as one-line notes; the pager renders correctly on the iOS 16.4 simulator (T-0374 leg) with no clipping
  against the T-0368 pill-bar inset contract.
- [ ] **AC4 (non-regression)** — Customer suite green; swiftformat/swiftlint --strict clean.

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

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
- 2026-07-03 — dev (harvest note): one row folded into `patterns-mobile.md` (D3 table): customer
  `BrandGradients.kt` → the Core `BrandGradient` token enum (dynamic pairs + fixed `.plusHero`,
  `linearGradient` top-leading→bottom-trailing = Compose's default), with the "models carry the semantic
  token so tests compare gradients by case" idiom.

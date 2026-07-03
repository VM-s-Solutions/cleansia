---
id: T-0368
title: "iOS customer shell restructure — single shell NavigationStack + page-style pager + the custom pill bar/FAB composite (fixes the iOS-16 Plus-route crash, the yellow-warning pushes, the never-ported island bar, the FAB overlap, and tab-swipe parity)"
status: in_progress
size: M
owner: ios
created: 2026-07-03
updated: 2026-07-03
depends_on: []
blocks: [T-0373]
stories: []
adrs: [ADR-0014, ADR-0018, ADR-0020]
layers: [architect, ios]
security_touching: false
priority: high
manual_steps: []
sprint: 12
source: phase/ios-fix1 on-device shakeout diagnosis (2026-07-02, cluster navigation-shell + the nested-stack finding of cluster data-layer)
---

> **SHIP-BLOCKER — iOS-16-specific.** The owner's first real-device run (iOS 16 iPhone) crashes on the
> Cleansia Plus route and renders bare yellow-⚠️ placeholder pages on Profile sub-screens. Every defect in
> this ticket was **invisible on the latest-runtime simulator** (iOS 17+ reworked the navigation authority
> and the modern system tab-bar styling masked the missing island-bar port) — see T-0374 for the process fix.
>
> **Architect ruling IN FLIGHT:** the single-stack + pager restructure **changes the ADR-0020 customer-shell
> pattern** (per-tab in-tab stacks → one shell-level stack + a page-style pager). The architect decides this
> FIRST (it structurally eliminates the crash class and hosts the custom bar — deciding it late means doing
> the NavigationPath conversion twice). If the restructure is REJECTED, fall back to the minimal crash fix
> (delete the outer stack + convert the four typed paths to `NavigationPath`) and split the bar/pager work out.

## Context
Four findings of the **navigation-shell** cluster plus the nested-stack finding of the **data-layer**
cluster, all sharing one root topology:

1. **CRASH tapping Cleansia Plus (`comparisonTypeMismatch`, iOS 16 only).** An outer pathless
   `NavigationStack` in `CustomerRootView.swift:16-19` wraps the shell's `TabView`, which holds four sibling
   `NavigationStack`s bound to four DIFFERENT homogeneous typed paths (`[HomeRoute]`/`[OrderRoute]`/
   `[RewardsRoute]`/`[ProfileRoute]`) — `CustomerShellView.swift:131,150,164,178`. On iOS 16 a programmatic
   push into any inner typed path makes SwiftUI compare `AnyNavigationPath` values of differing element types
   → `comparisonTypeMismatch` inside SwiftUI's internal `try!` (hardened on iOS 17+). Trigger sites:
   `CustomerShellView.swift:140` (Home banner → `.subscribePlus`), `:183` + `ProfileTab.swift:25` (Profile
   Plus card). Extra iOS-16 hazards: the multi-element path sets `:212` (`[.disputes,.disputeDetail]`) and
   `:333` (`[.recurringList,.createRecurring]`), and `OrderDetailView.swift:53`
   (`.navigationDestination(isPresented:)` mixed with homogeneous typed paths — fires on Order→Photos).
2. **Profile sub-screens render a bare page with a lone yellow ⚠️ (no message/retry).** Same nested topology:
   the outer stack's NavigationAuthority intercepts inner path pushes; unresolvable values render the iOS
   16.0–16.3 missing-destination placeholder. Empirically reproduced on the iOS 16.4 simulator (pushed screen
   COVERS the tab bar; console: "Update NavigationAuthority bound path tried to update multiple times per frame").
3. **The floating island/pill tab bar was never ported.** The shell uses the stock `TabView` + `.tabItem`
   (`CustomerShellView.swift:130-193`, zero `#available` branches); the island look the owner saw on the new
   simulator was the OS's own styling. Android reference: the hand-built floating pill — `MainShell.kt:363-407`
   (64dp pill, 16dp side margins, `RoundedCornerShape(32)`, outlineVariant border) floating above the gesture
   inset (`MainShell.kt:303-306`).
4. **Book FAB overlaps/clips tab items; Rewards content sits behind the bar.** The 64pt `BookFab` is overlaid
   on the whole `TabView` anchored to the DEVICE safe-area bottom with a fixed `-28` offset
   (`CustomerShellView.swift:101-104`, `:366-380`) — on 0pt-bottom-inset iOS-16-floor hardware (iPhone 8/8
   Plus) it lands on the 49pt system bar; no reserved center slot (Android reserves 72dp —
   `MainShell.kt:393`); tab content clearance is an ad-hoc 40pt trailing spacer (`RewardsTab.swift:93`,
   `HomeTab.swift:59`, `OrdersTab.swift:99`) vs Android's 88dp contract (`MainShell.kt:246,254-256`). Bonus:
   the FAB overlay floats over every PUSHED child screen (e.g. SubscribePlus's sticky CTA,
   `SubscribePlusScreen.swift:302-320`) — Android shows bar+FAB only on the shell.
5. **Tabs not swipeable.** Android drives the 4 tabs with a `HorizontalPager` (`MainShell.kt:93-99,259-296`)
   and hosts child screens ABOVE the shell; the iOS default-style `TabView` has no page-swipe gesture.

## Acceptance criteria
- [ ] **AC1 (crash gone)** — Given the iOS 16.4 simulator (and the owner's iOS 16 device), When Cleansia Plus
  is opened from the Home banner AND from the Profile card, Then SubscribePlus pushes with no
  `comparisonTypeMismatch` crash.
- [ ] **AC2 (⚠️ gone)** — Profile sub-screens (and every other push) reach their real destinations on iOS
  16.4 — no yellow-warning placeholder page; pushed children cover the whole shell (Android NavHost parity).
- [ ] **AC3 (topology)** — NO nested `NavigationStack`s remain: ONE shell-level stack owns all child routes
  (the four route enums merged into a single `ShellRoute` / one `NavigationPath`); the pager hosts ONLY the 4
  tab roots; the outer stack in `CustomerRootView` is deleted (auth screens use no nav-bar APIs — verified).
- [ ] **AC4 (pill bar)** — The custom pill bar renders identically on iOS 16 and latest: 64pt capsule,
  16pt horizontal margins, surface fill + outlineVariant stroke, animated selection dot, a reserved 72pt
  center slot; the system tab bar is hidden (`.toolbar(.hidden, for: .tabBar)`, iOS 16.0-safe).
- [ ] **AC5 (FAB composite)** — The FAB folds into the bar composite (top-center, offset −12, half-overlapping
  the pill — Android parity), mounted via `.safeAreaInset(edge: .bottom)` so every tab's scroll content
  auto-clears bar height + FAB overhang; the ad-hoc 40pt spacers and the shell-level FAB overlay are deleted;
  Rewards referral/activity cards are fully visible; bar+FAB do NOT appear on pushed child screens.
- [ ] **AC6 (tab swipe)** — Swiping left/right on a tab root changes tabs
  (`TabView(selection:)` + `.tabViewStyle(.page(indexDisplayMode: .never))`, iOS-14+-safe); pushed child
  screens are NOT swipeable between tabs.
- [ ] **AC7 (hazard sites)** — The dispute create→detail and recurring list→create multi-element pushes and
  OrderDetail's `isPresented` destination all work on iOS 16.4 (legalized by the type-erased single path).
- [ ] **AC8 (living docs)** — The architect folds the ruling into ADR-0020's living doc (customer shell =
  single shell stack + pager) and corrects the stale ADR-0018 D3 row (Android bottom bar is a CUSTOM pill,
  not Material NavigationBar → maps to the custom SwiftUI pill bar, replacing the recorded TabView mapping);
  Gate-DP records the mapping change.
- [ ] **AC9 (regression + smoke)** — CleansiaCore + CleansiaCustomer test suites green; iOS 16.4-simulator
  smoke of the diagnosis checklist: Plus from Home, Plus from Profile, order detail, dispute create→detail,
  membership success, order photos, profile sub-screens (T-0374 leg).

## Out of scope
- The partner app's mirrored outer-stack crash — **T-0369** (minimal fix, no restructure).
- The Home upsell carousel that mounts on the new pager — **T-0373** (depends on this + T-0372).
- The in-sheet snackbar/booking wizard fixes — **T-0371** (serialize edits: both tickets touch
  `CustomerShellView.swift`).

## Implementation notes
- Decide the architect ruling FIRST (single-stack + pager vs minimal fix). The restructure eliminates the
  crash class **by construction** (single stack, no nesting, no sibling typed paths) and is the only shape
  that hosts findings 3–5 without rework.
- Files at the center: `CustomerRootView.swift`, `CustomerShellView.swift`, `CustomerShellModel` (typed
  arrays → one `NavigationPath`; update `removeLast`/`isEmpty` call sites), `CustomerShellTab.swift`, a new
  shell bar component (pill + `NavSlot` items + FAB), tab roots' spacer cleanup.
- Zero `#available` forks needed — every API used is iOS-16.0-safe.
- Reviewer runs concurrently (reviewer-per-developer invariant); Gate-DP compares against
  `MainShell.kt` as the cited reference.

## Status log
- 2026-07-03 — filed `in_progress` by pm from the phase/ios-fix1 on-device shakeout diagnosis (16
  owner-reported issues, 4-cluster diagnosis; this slice carries 5 findings). **Architect ruling in flight**
  on the ADR-0020 shell-pattern change — dev proceeds on the ruling's shape; fallback recorded above.
  SHIP-BLOCKER priority (the Plus-route crash + the ⚠️ pushes are user-visible on every iOS 16 device).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

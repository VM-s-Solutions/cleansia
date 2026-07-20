---
id: T-0368
title: "iOS customer shell restructure — single shell NavigationStack + page-style pager + the custom pill bar/FAB composite (fixes the iOS-16 Plus-route crash, the yellow-warning pushes, the never-ported island bar, the FAB overlap, and tab-swipe parity)"
status: done
size: M
owner: ios
created: 2026-07-03
updated: 2026-07-03
depends_on: []
blocks: [T-0373]
stories: []
adrs: [ADR-0014, ADR-0018, ADR-0020, ADR-0022]
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
- [x] **AC1 (crash gone)** — Given the iOS 16.4 simulator (and the owner's iOS 16 device), When Cleansia Plus
  is opened from the Home banner AND from the Profile card, Then SubscribePlus pushes with no
  `comparisonTypeMismatch` crash. *(16.4 smoke: 0 hits; the owner-device half rides the phase device pass.)*
- [x] **AC2 (⚠️ gone)** — Profile sub-screens (and every other push) reach their real destinations on iOS
  16.4 — no yellow-warning placeholder page; pushed children cover the whole shell (Android NavHost parity).
- [x] **AC3 (topology)** — NO nested `NavigationStack`s remain: ONE shell-level stack owns all child routes
  (the four route enums merged into a single `ShellRoute` / one `NavigationPath`); the pager hosts ONLY the 4
  tab roots; the outer stack in `CustomerRootView` is deleted (auth screens use no nav-bar APIs — verified).
- [x] **AC4 (pill bar)** — The custom pill bar renders identically on iOS 16 and latest: 64pt capsule,
  16pt horizontal margins, surface fill + outlineVariant stroke, animated selection dot, a reserved 72pt
  center slot. *(DEVIATION, recorded: NO `.toolbar(.hidden, for: .tabBar)` — under `.page` style no system
  tab bar exists; ADR-0022 D3 rules the modifier dead, don't add it.)*
- [x] **AC5 (FAB composite)** — The FAB folds into the bar composite (top-center, offset −12, half-overlapping
  the pill — Android parity), mounted via `.safeAreaInset(edge: .bottom)` so every tab's scroll content
  auto-clears bar height + FAB overhang; the ad-hoc 40pt spacers and the shell-level FAB overlay are deleted;
  Rewards referral/activity cards are fully visible; bar+FAB do NOT appear on pushed child screens.
  *(DEVIATION, recorded: the FAB is Android's true **74pt/34pt glyph**, not the AC's 64pt — ADR-0022 had
  mis-transcribed its own cited source; corrected via the Slice-D carrier `fef5745c`; see T-0379.)*
- [x] **AC6 (tab swipe)** — Swiping left/right on a tab root changes tabs
  (`TabView(selection:)` + `.tabViewStyle(.page(indexDisplayMode: .never))`, iOS-14+-safe); pushed child
  screens are NOT swipeable between tabs.
- [x] **AC7 (hazard sites)** — The dispute create→detail and recurring list→create multi-element pushes and
  OrderDetail's `isPresented` destination all work on iOS 16.4 (legalized by the type-erased single path).
- [x] **AC8 (living docs)** — The architect folds the ruling into ADR-0020's living doc (customer shell =
  single shell stack + pager) and corrects the stale ADR-0018 D3 row (Android bottom bar is a CUSTOM pill,
  not Material NavigationBar → maps to the custom SwiftUI pill bar, replacing the recorded TabView mapping);
  Gate-DP records the mapping change. *(Delivered as **ADR-0022** + the living-doc/checklist fold-in,
  `987f85f0`.)*
- [x] **AC9 (regression + smoke)** — CleansiaCore + CleansiaCustomer test suites green; iOS 16.4-simulator
  smoke of the diagnosis checklist: Plus from Home, Plus from Profile, order detail, dispute create→detail,
  membership success, order photos, profile sub-screens (T-0374 leg). *(Suites green on BOTH runtimes; the
  16.4 smoke ran boot-install-launch + the routing tests — the SIGNED-IN walk of the checklist is the owner
  device pass, flagged in the PR; no credentials in the harness.)*

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
- 2026-07-03 — architect ruling ACCEPTED as **ADR-0022** (single shell-level `NavigationStack` + `.page`
  pager + the custom pill-bar/FAB composite; nested stacks are henceforth a defect class) — docs commit
  `987f85f0` (the ADR + the ADR-0020/0018 living-doc fold-ins + the Gate-DP AR-DP-1a/AR-DP-4 hardening =
  AC8). Dev proceeded on the ruling's shape; the fallback was never needed.
- 2026-07-03 — implemented by ios (Slice A) in `d34eaf5e`: the 4 route enums merged into one 18-case
  `ShellRoute` (deduping the twice-registered destinations) on a type-erased `NavigationPath`; the outer
  stack in `CustomerRootView` deleted; the 4 tab roots on `TabView(.page)` (swipe parity; pill taps animate
  the pager); `CustomerBottomBar` ported (64pt pill, 16pt margins, r32, outline stroke, animated selection
  dots, 72pt reserved center gap, the BookFab half-overlapping at −12) mounted via `.safeAreaInset(.bottom)`
  — the ad-hoc 40pt spacers and the overlay FAB deleted; pushed children cover the whole shell. One
  deliberate Android-cited dedup: BOTH subscribePlus entries now replace the paywall with MembershipSuccess
  on subscribe (`CleansiaNavHost.kt:540-546` parity — the old Profile path popped silently, a pre-existing
  divergence resolved). 7 new `CustomerShellRoutingTests` (incl. the 18-route erased-path round-trip).
  Commit-time evidence: Core 253 / Customer 388 / Partner 371 green (iPhone 17); swiftformat 0.60.1 +
  swiftlint --strict clean; iOS 16.4 smoke — NavigationAuthority hits 0, comparisonTypeMismatch 0.
- 2026-07-03 — review reconciled (workflow-verified; findings ROUTED, none a Slice-A code defect): the shell
  snackbar CLEARANCE over the new bar composite and the 74pt FAB transcription correction were routed to
  Slice D as carrier — both folded in `fef5745c` (`SnackbarController.bottomInset` + `ShellSnackbarInset`
  100pt; FAB 74/34 + the ADR/living-doc transcription notes → architect ratification tracked as T-0379).
- 2026-07-03 — **done** by pm at phase close. Final-tree gates: Core 272/272 on BOTH iPhone 17 (iOS 26.3)
  AND the iOS 16.4 floor sim; Customer 406/406; Partner green; swiftformat 0.60.1 0/528 + swiftlint --strict
  clean tree-wide; iOS 16.4 boot-install-launch smoke of BOTH apps with 0 NavigationAuthority/
  comparisonTypeMismatch hits. REMAINING acceptance: the owner's on-device pass (Plus from Home + Profile,
  order detail, dispute create→detail, tab swipe, bar hidden on push) — flagged in the phase PR.
- 2026-07-03 — fix-round 2 by ios (**owner directive** from the on-device pass): the pill's opaque
  surface fill replaced with translucent material — `.glassEffect(.regular, in:
  RoundedRectangle(cornerRadius: 32))` under `#available(iOS 26.0, *)` (compiles against the Xcode 26.3
  SDK), `.ultraThinMaterial` in a `Capsule` below — every other D3 geometry element kept (64pt pill,
  16pt margins, r32, outline-variant stroke, selection dots, 72pt center gap, opaque-primary FAB).
  Recorded as the dated owner-directed amendment note under ADR-0022 D3 (overrides the
  copy-Android-exactly fill; Android parity for the fill is intentionally broken by owner order).
  Evidence: Customer 408/408 green on iPhone 17; swiftformat + swiftlint --strict clean on the file.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
- 2026-07-03 reviewer (Slice A, concurrent): **workflow-verified** — the restructure checked against
  ADR-0022 D1–D3 and the cited `MainShell.kt` reference (Gate-DP); findings ROUTED, not blocking:
  (1) tab-root snackbars need a bar-composite clearance (→ Slice-D carrier, folded `fef5745c`);
  (2) the ADR's 64pt FAB line contradicts its own cited source (`MainShell.kt:456-462` = 74dp/34dp)
  (→ Slice-D carrier + the ADR transcription note; ratification → T-0379). PM reconciled 2026-07-03:
  both findings landed; slice advanced.
- 2026-07-19 architect (T-0379 close-out): finding (2)'s ADR-0022 in-body 74pt note is **ratified as a
  signed erratum** (block appended to ADR-0022; convention in `agents/backlog/adr/README.md`) — the
  supersede-never-edit flag this review raised is resolved. See T-0379 Review for the full verdicts.

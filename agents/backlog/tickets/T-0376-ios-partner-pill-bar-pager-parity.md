---
id: T-0376
title: "iOS partner shell — pill-bar + pager parity (the ADR-0022 D4-mandated follow-up): adopt the D2/D3 shape with the partner pill variant + harvest the shared pill to CleansiaCore (second call site)"
status: proposed
size: M
owner: ios
created: 2026-07-03
updated: 2026-07-03
depends_on: []
blocks: []
stories: []
adrs: [ADR-0014, ADR-0018, ADR-0020, ADR-0022]
layers: [ios]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: ADR-0022 D4 (the "PM-filed follow-up ticket" the ruling mandates) — phase/ios-fix1 deferral
---

> **This is the follow-up ADR-0022 D4 explicitly mandates the PM to file.** In phase/ios-fix1 the partner
> app took ONLY the minimal crash fix (T-0369: outer stack deleted + `NavigationPath` conversion) — but the
> Android partner app DOES ship the pill + pager (`FloatingIslandBottomBar.kt` — with the in-code comment
> "the two apps share a visual home so the bar must read as the same component" — hosted over a
> `HorizontalPager`), while the iOS partner still uses the stock `TabView`. Until this ticket lands, the
> partner's per-tab `NavigationPath` stacks + stock bar remain the ADR-0022-sanctioned RECORDED INTERIM
> (D4), i.e. a known, recorded divergence — not a defect. Dedup check done at filing: no existing ticket
> covers this (INDEX + tickets grep for partner pill/pager: none).

## Context
ADR-0022 (accepted 2026-07-02) restructured the CUSTOMER shell: one shell-level `NavigationStack` +
`.page` pager + the custom pill-bar composite (D2/D3), classifying the floating pill as the apps' shared
BRAND SIGNATURE (ADR-0018 branding — non-negotiable parity). D4 scoped the partner to the minimal crash
fix for that phase and mandated this ticket for the parity leg. Alternatives-considered #5 additionally
rules WHEN the shared pill is harvested to `CleansiaCore`: at the SECOND call site — this ticket — per the
≥2-call-sites rule (the customer's `CustomerBottomBar` was deliberately built app-local).

## Acceptance criteria
- [ ] **AC1 (topology)** — The partner shell adopts the ADR-0022 D2 shape: ONE shell-level
  `NavigationStack` owning all child routes (the per-tab route enums merge into a single partner
  `ShellRoute` on one type-erased `NavigationPath`; the in-tab stacks in OrdersList/Earnings/Profile are
  deleted), tab ROOTS on `TabView(selection:)` + `.tabViewStyle(.page(indexDisplayMode: .never))`; pushed
  children cover the whole shell. The RegistrationLock keeps its OWN stack (T-0310 gate #24,
  byte-unchanged — it is a sibling under the root switch, not a nesting).
- [ ] **AC2 (pill bar, partner variant)** — The D3 pill composite with the partner variant per
  `FloatingIslandBottomBar.kt`: 4 EVEN slots, NO FAB, NO reserved center gap; same pill metrics as the
  customer (64pt capsule, 16pt margins, r32, surface fill + outlineVariant stroke, animated selection
  dot); mounted via `.safeAreaInset(edge: .bottom)`; bar hidden on pushed children.
- [ ] **AC3 (Core harvest)** — The shared pill SHAPE is hoisted to `CleansiaCore` (the ≥2-call-sites rule,
  ADR-0022 alternative #5): one Core pill component parameterized for the customer (center gap + FAB
  overhang) and partner (4 even slots) variants; `CustomerBottomBar` repoints with byte-equivalent
  rendering; the harvest is recorded in `patterns-mobile.md`.
- [ ] **AC4 (tab swipe)** — Partner tabs swipe left/right on tab roots; pushed children are NOT swipeable
  between tabs (customer parity).
- [ ] **AC5 (non-regression + floor)** — Core + Partner + Customer suites green; swiftformat/swiftlint
  --strict clean; the iOS 16.4 floor leg (T-0374): launch + push smoke with 0
  NavigationAuthority/comparisonTypeMismatch hits; Gate-DP cites `FloatingIslandBottomBar.kt` + the
  partner `MainShell` pager as references.

## Out of scope
- Any FAB on the partner shell (Android partner has none).
- The customer shell (done, T-0368) — except the AC3 repoint, which must be render-identical.
- ADR changes: this ticket COMPOSES ADR-0022 (D4 pre-authorizes exactly this scope) — no new decision,
  no panel (no-decision note); if implementation forces a D2/D3 deviation, STOP and route to the
  architect first.

## Implementation notes
- Follow T-0368's landed shape (`ShellRoute` merge, `CustomerShellRoutingTests` pattern, the
  `safeAreaInset` clearance contract) — copy the proven structure, don't re-derive it.
- Mind the T-0371/T-0369 snackbar + deep-link surfaces: the partner deep-link resolver
  (`deepLinkRoute(orderId)`) re-targets the merged `ShellRoute`; the shell snackbar lift
  (`SnackbarController.bottomInset`) gets its partner twin if the partner shell emits over the new bar.
- Serialize with any other ticket editing the partner shell files.
- Reviewer runs concurrently (reviewer-per-developer invariant).

## Status log
- 2026-07-03 — filed `proposed` by pm at the phase/ios-fix1 close — the ADR-0022 D4-mandated follow-up
  (dedup-checked: not previously filed). Medium priority: no crash (T-0369 fixed that), pure
  brand/interaction parity; the interim stock bar is ADR-recorded. Not dispatched.
- 2026-07-17 — ios: STOPPED without code per the ticket's own out-of-scope rule ("if implementation
  forces a D2/D3 deviation, STOP and route to the architect first"). ADR-0022's owner-directed
  supersede (2026-07-08, phase/ios-fix2, `365fd221`) retired the D2 pager + D3 pill/FAB composite for
  the customer shell (pill corrupted on the owner's iOS 26 device; stock `TabView` restored) and states
  this ticket "is effectively cancelled by this supersede — flag for the PM to retire it". The harvest
  source is gone from the tree (`CustomerBottomBar.swift` now holds only the surviving `BookFab`;
  customer shell is stock `TabView`), so AC2/AC3/AC4 are unimplementable as scoped and the ≥2-call-sites
  harvest premise no longer holds (one prospective call site). Both shells currently match on the stock
  bar — building the partner pill would create the cross-app divergence this ticket existed to remove.
  Routed to the PM to retire (or the architect to re-scope, e.g. a stock-TabView-parity leg: partner
  D2 single-shell-stack topology only, if still wanted). Note for the PM/architect: the INDEX row and
  the living-doc rows (`ios-app-architecture.md:79-80,895`) still describe the pre-supersede pill
  mandate — stale against the ADR's 2026-07-08 supersede section.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

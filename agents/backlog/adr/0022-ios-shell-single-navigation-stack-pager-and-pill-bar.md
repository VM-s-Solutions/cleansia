# ADR-0022 — iOS shell navigation: ONE shell-level `NavigationStack` + a page-style tab pager + the custom pill-bar/FAB composite (no outer root stack)

- **Status:** accepted (2026-07-02)
- **Author:** architect (phase/ios-fix1 ruling — the owner-device iOS 16 defect sweep)
- **Supersedes:** the sprint-12 §7.15 Decision 6 shell mapping ("Android floating pill `CustomBottomBar` + `BookFab` →
  native `TabView` + FAB overlay is the sanctioned ADR-0018 D3 swap") — REVERSED for the shells. The ADR-0018
  *principle* is unchanged; its D3 "bottom `NavigationBar` → `TabView`" table row is **corrected** in the living doc
  (the row's left column was factually wrong — neither Android app's shell is a Material `NavigationBar`).
- **Relates:** ADR-0013/0014 (parity port, iOS-16 floor), ADR-0018 (design parity — the branding boundary this
  enforces), ADR-0020 (root router — **unchanged**), ADR-0021 (modal vs non-modal sheets — **unchanged**).

## Context

On the owner's iOS 16 iPhone (the ADR-0014 floor — the reason iOS 16 exists as a target) the customer app **fatally
crashes** opening Cleansia Plus (`comparisonTypeMismatch` inside SwiftUI's navigation authority) and renders **bare
yellow-⚠️ missing-destination placeholder pages** on Profile pushes. Empirically reproduced on the iOS 16.4 sim
(diagnosis, phase/ios-fix1): `CustomerRootView.swift:17` wraps everything — including `CustomerShellView`'s FOUR
sibling `NavigationStack`s bound to four different homogeneous typed paths (`[HomeRoute]`/`[OrderRoute]`/
`[RewardsRoute]`/`[ProfileRoute]`, `CustomerShellView.swift:131,150,164,178`) — in an outer pathless
`NavigationStack`. iOS 17+ tolerates the nesting (which is why sim suites on the latest runtime never caught it);
iOS 16 punishes it. Two further documented iOS-16 hazards are live: multi-element path sets in one transaction
(`CustomerShellView.swift:212,333`) and `.navigationDestination(isPresented:)` mixed with homogeneous typed paths
(`OrderDetailView.swift:53`). `PartnerRootView.swift:17` carries the identical outer-stack topology over four typed
in-tab stacks (`OrdersListView.swift:35`, `EarningsView.swift:26`, `ProfileView.swift:47`,
`RegistrationLockView.swift:38`).

Separately, the shell never reached design parity: **both** Android shells are a `HorizontalPager` (swipeable tabs,
children hosted ABOVE the shell in the `NavHost`) with a hand-built floating pill bar — customer
`MainShell.kt:363-407` (`CustomBottomBar`, 64dp pill, 16dp margins, outline stroke, 72dp reserved center slot,
`BookFab` at top-center offset −12) and partner `FloatingIslandBottomBar.kt` (same pill, 4 even slots, no FAB, with
the in-code comment *"the two apps share a visual home so the bar must read as the same component"*). iOS shipped a
stock `TabView` + `.tabItem` (§7.15 D6 sanctioned it as a component swap); the modern-OS system tab-bar styling
masked the gap on the sim, and on iOS 16 the owner got the legacy full-width bar, a FAB colliding with the middle
tab items (fixed −28 offset against the DEVICE safe area — 0pt on button-home floor hardware), content under the
bar, and no swipe.

## Decision

**D1 — Delete the outer root `NavigationStack`s.** `CustomerRootView.body` (line 17) and `PartnerRootView.body`
(line 17) become the bare flat-enum route `switch` (ADR-0020's actual shape — the root-switch never needed a stack;
no auth screen uses nav-bar APIs). After this ADR, **a `NavigationStack` nested inside another `NavigationStack` is
a defect class, never a pattern**, anywhere in either app.

**D2 — Customer shell restructure (the approved option): ONE shell-level `NavigationStack` owning ALL child routes.**
- The four typed route enums merge into **one `ShellRoute` enum** (deduping the twice-registered
  `subscribePlus` and order-detail destinations), registered **once** via
  `.navigationDestination(for: ShellRoute.self)`.
- The path is held as a **type-erased `NavigationPath`** (`@Published var path = NavigationPath()`), appending
  `ShellRoute` values — not `[ShellRoute]`. The erased container removes the homogeneous-path comparison code path
  outright and legalizes the two multi-element sets and `OrderDetailView`'s `isPresented` destination on iOS 16.
- The 4 tab ROOTS (and only they) are hosted in
  `TabView(selection:)` + `.tabViewStyle(.page(indexDisplayMode: .never))` (iOS 14+) — Android's swipe parity.
  Pushed children land on the shell stack and **cover the whole shell** (bar + FAB + pager) — the Android
  NavHost-above-shell behavior, and it makes cross-tab swiping on a pushed child structurally impossible.
- One back stack, exactly like Android. Per-tab preserved push-stacks (the stock-iOS behavior) are **given up**;
  see Consequences.

**D3 — The pill-bar/FAB composite is BRANDING, mounted via the safe-area contract.** A `CustomerBottomBar`
composite (the `CustomBottomBar` + `BookFab` port: 64pt pill, 16pt horizontal margins, corner radius 32, surface
fill + 1pt outline-variant stroke, 4 `NavSlot`s with the animated selection dot, a reserved 72pt center gap, the
74pt `BookFab` (34pt glyph) at top-center offset −12 *[transcription-corrected 2026-07-03: this line originally
said 64pt, but its own cited source `MainShell.kt:456-462` is `Modifier.size(74.dp)` + a 34dp icon — D3's
copy-Android-exactly ruling governs]*) is mounted with **`.safeAreaInset(edge: .bottom)`** on the pager — every
tab's `ScrollView` is automatically inset by the composite's full height (pill + FAB overhang, the Android 88dp
clearance-contract analogue). The ad-hoc 40pt trailing spacers and the shell-level `.overlay` + `offset(y:-28)` FAB
are deleted. Under `.page` style **no system tab bar exists**, so no `.toolbar(.hidden, for: .tabBar)` is needed —
do not add dead modifiers. Classification ruling: the floating pill + center FAB is the apps' **shared brand
signature** (the partner Android file says so in code), i.e. ADR-0018 **branding/layout — non-negotiable parity** —
not a swappable "component". §7.15 D6's contrary classification is superseded.

*[Owner-directed amendment, 2026-07-03 (fix-round 2):* the pill's **opaque surface fill** is replaced by
**translucent material** — Liquid Glass (`.glassEffect`) on iOS 26+, `.ultraThinMaterial` (Capsule) below —
keeping every other D3 geometry element (64pt pill, 16pt margins, radius 32, stroke, dots, opaque-primary FAB);
an explicit owner override of the copy-Android-exactly fill.*]*

**D4 — Partner scope THIS phase: the minimal crash fix only.** Delete the outer stack (D1) + convert the four typed
`[Route]` paths to `NavigationPath` (keeping every `.navigationDestination(for:)` registration and the in-tab-stack
topology recorded in §7.7 D1 / §7.12 D1). Partner pill/pager parity (Android partner **does** use
`HorizontalPager` + `FloatingIslandBottomBar`) is a **PM-filed follow-up ticket** that adopts THIS ADR's D2/D3
shape with the partner pill variant (4 even slots, no FAB, no center gap). Until that ticket lands, the partner's
per-tab `NavigationPath` stacks remain sanctioned as the recorded interim.

**D5 — Gate-DP hardening (the gate misses this defect cluster exposed).** Two additions to
`ios-app-review-checklist.md` §G: **AR-DP-1a** (every drawable/raw asset the cited Android screen references has an
iOS asset-catalog counterpart; SF-symbol substitution allowed ONLY for Material icon vectors, NEVER for brand
raster/animated art) and **AR-DP-4** (a one-time per-app app-chrome check: AppIcon + branded launch screen +
in-app splash — owned by the app's shell/scaffold ticket, since app chrome lives in no screen's `.kt` citation).
The AR-DP-3 canonical-mapping line drops "Compose bottom-nav → `TabView`".

## Alternatives considered

1. **Minimal fix only for the customer too** (delete outer stack + per-tab `NavigationPath` conversion; bar/FAB/
   pager as separate work) — rejected as the customer answer. It patches the crash but leaves the three
   owner-reported parity defects, and the bar/FAB/pager work then restructures the same shell again (paying the
   path conversion twice — the diagnosis's own sequencing point). It IS the right partner scope this phase (D4),
   where no restructure is scheduled.
2. **Bump the floor to 16.4/17** — rejected. Re-opens ADR-0014's 2017-device reach (the owner's explicit priority),
   and fixes only the crash, not the bar/FAB/swipe parity.
3. **Keep per-tab stacks and add a pager around them** — rejected as structurally wrong: pushed children would live
   inside pages (swipeable between tabs mid-flow — the exact anti-parity), and the sibling typed stacks remain the
   iOS-16 crash surface.
4. **Customize the system tab bar** (UIKit appearance / `UITabBarController`) — rejected: the system bar cannot
   host the reserved center gap, the FAB overhang, or page-swipe; it fights UIKit chrome across OS versions instead
   of owning 100 lines of SwiftUI.
5. **Hoist the pill bar to `CleansiaCore` now** — deferred (the §7.6 D1 anti-speculation precedent): build
   `CustomerBottomBar` app-local; when the partner parity follow-up creates the second call site, harvest the shared
   pill shape to Core then (the ≥2-call-sites rule), not before.

## Consequences

- The iOS-16 crash class is eliminated **by construction** (one stack, no nesting, no sibling typed paths, erased
  path container) — not by dodging trigger sites.
- Per-tab push-stack preservation on tab switch is lost. Conceded: it matches the cited Android reference exactly
  (one back stack; switching tabs happens only at the shell). ADR-0018 holds **navigation structure/flow**
  identical to Android — the per-tab stacks were the divergence.
- Tab roots stay alive in the pager while the shell is mounted (scroll positions survive pushes and tab switches —
  the shell stack keeps its root mounted under pushed children).
- ADR-0020 is **unchanged**: the audience root stays a flat-enum switch; `NavigationStack` stays the intra-audience
  push container — there is now exactly ONE of them per signed-in audience.
- ADR-0021 is **unchanged**: the booking wizard stays a modal `.sheet` presented from the shell root; sheets and
  pushes no longer fight the FAB overlay (the composite lives at the shell root only).
- Tab roots must not drive the shell's nav bar: the shell root hides the navigation bar
  (`.toolbar(.hidden, for: .navigationBar)` on the root content); `ProfileTab.swift:44`'s
  `.navigationTitle(L10n.Shell.profile)` moves to an in-content header (cite the Android profile tab). Pushed
  children keep their `.navigationTitle` + back affordances.

## How a reviewer verifies compliance

1. `grep -rn "NavigationStack" src/cleansia_ios/CleansiaCustomer/Sources` → exactly **one** occurrence in the
   signed-in hierarchy (the shell); **none** in `CustomerRootView` around `content`. Partner: none in
   `PartnerRootView` around `content`.
2. One `.navigationDestination(for: ShellRoute.self)` registration; the enums `HomeRoute`/`OrderRoute`/
   `RewardsRoute`/`ProfileRoute` are gone; the shell model's path is `NavigationPath`, not `[SomeRoute]`
   (partner interim: the four paths are `NavigationPath`).
3. The pager is `TabView(selection:)` + `.tabViewStyle(.page(indexDisplayMode: .never))` hosting only the 4 tab
   roots; the bar mounts via `.safeAreaInset(edge: .bottom)`; no `.overlay` + fixed-offset FAB; no ad-hoc bottom
   clearance spacers; no `.toolbar(.hidden, for: .tabBar)` dead modifier.
4. On an iOS 16.x device/sim: Plus from Home banner, Plus from Profile card, order detail, dispute create→detail,
   membership success, order photos — no crash, no yellow-⚠️ placeholder, bar+FAB hidden on every push, tabs swipe.
5. §G carries AR-DP-1a + AR-DP-4; AR-DP-3's mapping list no longer offers `TabView` for the shell bar.

## Challenge

1. *"This reverses a recorded, reviewed decision (§7.15 D6) — the stock `TabView` was ruled the sanctioned
   iOS-wins swap. Why is re-litigating it not churn?"*
2. *"A single shell stack throws away per-tab back stacks — a genuinely iOS-native affordance ADR-0018's
   'iOS convention wins' clause should protect."*
3. *"Why the type-erased `NavigationPath`? A single typed `[ShellRoute]` array is one type — no sibling mismatch —
   and keeps typed introspection."*
4. *"Partner gets the crash fix but not the parity — the pill is supposedly the shared brand signature, so shipping
   the partner on the stock bar contradicts D3's own classification."*

## Defense

1. §7.15 D6 was made **against the wrong left column** — it cited the D3 row "bottom `NavigationBar` → `TabView`",
   but neither Android shell is a Material `NavigationBar`; both are hand-built brand chrome (the partner file's
   comment makes the intent explicit). ADR-0018's conflict rule gives iOS the win **on the component only, never on
   branding** — the ruling boundary was crossed, the owner rejected the result on first device contact, and the
   correction runs *toward* ADR-0018, not away from it. Superseding a mistaken application is the sanctioned
   mechanism (this ADR), not churn.
2. Conceded as a real cost — and accepted deliberately: ADR-0018 holds **navigation structure + user flow**
   identical to Android (non-negotiable), and Android has ONE back stack with children above the shell. Per-tab
   stacks were an unnoticed structural divergence that also happened to be the crash surface. Where parity and the
   iOS affordance conflict on *structure*, parity wins by the ADR's own hierarchy.
3. A single typed array does eliminate the sibling-type mismatch, but two *other* documented iOS-16 crash/glitch
   sources remain against homogeneous typed paths: multi-element sets in one transaction (used at
   `CustomerShellView.swift:212,333`) and mixing `.navigationDestination(isPresented:)` (`OrderDetailView.swift:53`).
   The erased `NavigationPath` retires all three for the cost of `removeLast()/isEmpty` call-site updates — this
   model appends, replaces, and pops; it never introspects the path. Belt-and-braces on the floor OS is worth the
   trivial API downgrade.
4. The partner crash fix is ship-blocking and small (four path vars + one deleted stack); the partner pill/pager is
   a coherent shell rework needing its own Gate-DP citation (`MainScaffold.kt` + `FloatingIslandBottomBar.kt`) and
   its own device pass. The customer restructure proves the D2/D3 shape first — the same lead-app sequencing logic
   as ADR-0013 (prove the architecture on one app, copy it). The interim stock partner bar is a *known, recorded*
   divergence with a filed successor — not a silent one.

## Verdict

Accepted 2026-07-02. Challenges 1 and 3 rebutted with evidence; challenge 2 conceded and priced (parity wins on
structure); challenge 4 conceded as sequencing, bounded by the PM-filed partner follow-up. The living doc
(`agents/architecture/decisions/ios-app-architecture.md`), `ios-app-review-checklist.md` §G, and
`patterns-mobile.md` are updated in the same change.

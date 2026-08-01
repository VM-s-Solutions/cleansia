---
id: T-0453
title: Android customer profile hero starts below the status bar; iOS bleeds the gradient under it
status: draft
size: M
owner: architect
created: 2026-07-30
updated: 2026-07-30
depends_on: [T-0448]
blocks: []
stories: []
adrs: []
layers: [architect, android]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

Reviewers on T-0442 called this **the most visible remaining delta between the two phones** once the
edit chip was centred. It was deliberately left out of T-0442's scope, and T-0442's `## Out of scope`
holds — converging it is not a padding tweak, it is an inset-ownership change.

**PM verification, 2026-07-30 — read both files:**

| | iOS | Android |
|---|---|---|
| Who owns the top inset | the **hero itself**: `HeroGradient` takes `topInset: proxy.safeAreaInsets.top` (`CleansiaCustomer/.../Profile/ProfileTab.swift:31`, `:188`, `:264`) and spends it **inside** its own padding — `.padding(.top, 48 + topInset)` at `:306`, with the gradient applied as `.background(...)` at `:309-311` | the **scroll container**: `Column(...).windowInsetsPadding(WindowInsets.statusBars)` at `customer-app/.../features/profile/ProfileTab.kt:135`, **plus** a deliberate `Spacer(Modifier.height(12.dp))` at `:141` with the comment *"Visual breathing room between the status bar and the hero gradient so the hero reads as a card on the page rather than abutting the system bar"* |
| Net effect | gradient paints **under** the status bar, content sits below it | gradient **starts below** the status bar, with 12dp of page background above it |

So the fix is: move the inset from the scroll `Column` into `ProfileHero`'s own padding, and delete
the 12dp spacer — which changes **who owns the top inset for the whole tab**, and the Android comment
at `:138-140` says the current behaviour is intentional and that *other tabs get their inset naturally
via their headline padding*. That is why this is an architect call and not a one-line diff:
`MainShell.kt` already owns the **bottom** inset centrally (`WindowInsets.navigationBars` at
`MainShell.kt:260`, applied at `:311`) while the **top** inset is per-tab. Whatever is decided here
sets the pattern for Home / Orders / Rewards too.

**Second-order consequence, must not be missed.** Once the gradient runs under the status bar, the
status-bar **icons** sit on brand blue instead of on `background`. `CleansiaTheme` sets the appearance
globally via `WindowCompat.getInsetsController(window, view)`
(`customer-app/.../ui/theme/Theme.kt:84`) keyed to the theme, not to the current tab — so in light
mode the icons would be dark-on-blue. Any AC that ignores this ships a legible-parity regression while
fixing a visual-parity one.

Edge-to-edge is already on: `MainActivity.kt:88` calls `enableEdgeToEdge()`, so the window already
extends under the bars — nothing new needs enabling.

## Acceptance criteria

- [ ] **AC1** — Given the Android customer profile tab, When it renders, Then the hero gradient paints
      **continuously from the top of the window**, under the status bar, with no page-background band
      above it — matching iOS `ProfileTab.swift:306`+`:309`. Evidence: side-by-side iOS/Android
      screenshots including the status bar, light **and** dark.
- [ ] **AC2** — Given the hero now paints under the status bar, When the status-bar icons render over
      it, Then they meet the contrast floor in **both** themes. Evidence: the icon-appearance decision
      (light-content over the gradient) named, plus screenshots in both themes. State whether the
      appearance is set per-tab or globally and what that does to the other three tabs.
- [ ] **AC3** — Given the user scrolls the profile tab, When the hero scrolls up out of view, Then
      whatever is under the status bar afterwards is legible — i.e. the icon appearance is not left
      pinned to a gradient that is no longer there. Evidence: a scrolled screenshot in both themes.
- [ ] **AC4** — Given the other three tabs (Home, Orders, Rewards), When they render after this
      change, Then each is **unchanged** or its change is **explicitly recorded with a reason**. The
      inset comment at `ProfileTab.kt:138-140` claims they get theirs from headline padding — verify
      that, do not trust it. Evidence: before/after screenshots of all four tabs.
- [ ] **AC5** — Given a device with a **cutout/notch** and one with a tall gesture area, When the tab
      renders, Then the hero content clears the cutout. Evidence: emulator run with a cutout profile.
- [ ] **AC6** — Gate 0.5: `:core` + `:customer-app` `compileDebugKotlin` + `testDebugUnitTest` run
      **un-cached** (`--rerun-tasks`), task outcomes recorded, and **not** `UP-TO-DATE`. Leg 3: this
      ticket's evidence is visual, so leg 1 does not apply — say so explicitly rather than inventing
      a mutation.

## Out of scope

- The **partner** Android app's profile screen.
- Converging the remaining T-0442 delta rows (padding tokens, type slots). Those were adjudicated
  under T-0442 AC2 and are closed.
- The bottom/navigation-bar inset — already centrally owned by `MainShell.kt:260,311` and correct.
- iOS. iOS is the reference here; it does not change.

## Implementation notes

**Architect panel required before this leaves `draft`.** The decision is not "should the gradient
bleed" (the owner already ruled iOS is the reference); it is **where the top inset lives in the
Android customer shell**. At least these options, with why-not for each:
(a) hero-owned, per-tab — mirrors iOS exactly, but four tabs each grow an inset concern;
(b) shell-owned in `MainShell` with an opt-out for gradient tabs — symmetric with how the bottom
inset is already handled at `MainShell.kt:260`, but adds a per-tab flag;
(c) `Modifier.consumeWindowInsets` on the pager with the hero re-applying — the idiomatic Compose
answer, and the one most likely to interact badly with the scroll container.
The panel must also rule on the status-bar-icon appearance seam (per-tab vs global at `Theme.kt:84`).
Record in `agents/architecture/decisions/`.

**Shared-file lane — this is deliberately last.** `ProfileTab.kt` runs
**T-0442 (done) → T-0450 → T-0448 → T-0453**. T-0453 is sequenced **after T-0448** on purpose: the
owner has ruled the avatar is part of the demo, so T-0448 is on the demo critical path and nothing
non-blocking may be inserted in front of it. Restructuring the hero *after* the avatar image lands
also means the restructure is done against the final hero, not against a placeholder that is about to
change. `depends_on: [T-0448]` encodes that.

## Status log
- 2026-07-30 — draft (created by pm; wave-1 finding with no home, from T-0442's reviewers; needs an
  architect panel on inset ownership; sequenced behind T-0448 so it does not extend the demo path)

## Review
<!-- reviewer writes verdict here -->

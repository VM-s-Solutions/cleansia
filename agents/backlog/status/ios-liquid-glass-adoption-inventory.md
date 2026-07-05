# iOS Liquid Glass adoption inventory — phase/ios-fix1 (for PM re-homing into a follow-up ticket)

- **Author:** ios (phase/ios-fix1, owner glass-parity request on the customer bottom bar)
- **Date:** 2026-07-04
- **Implemented in this phase:** the bar family only (`CustomerBottomBar` pill + Book FAB — see below).
  Everything else in the table is analysis for a PM-filed follow-up; nothing else was touched.
- **SDK facts (Xcode 26.3):** `glassEffect(_:in:)`, `Glass` (`.regular`/`.clear` + `.tint(Color?)` +
  `.interactive(Bool)`), `GlassEffectContainer(spacing:)`, `glassEffectID/Union/Transition`, and the
  `.glass`/`.glassProminent` button styles are ALL `@available(iOS 26.0, *)`. The ADR-0014 floor is
  iOS 16, so every adoption is an `#available(iOS 26.0, *)` branch with a fallback that must look
  native-intentional on 16–25.

## Adoption principles (from Apple's Liquid Glass guidance + this codebase's rules)

1. Glass belongs on **floating chrome above content** (bars, FABs, floating controls) — never on
   content cards, dialogs, or in-scroll surfaces.
2. Overlapping/adjacent glass shapes must share ONE `GlassEffectContainer` (glass cannot sample
   glass); system glass draws its own rim lighting, so hand-drawn 1pt strokes are dropped under the
   iOS 26 branch and kept on the classic branch.
3. Every adoption keeps the current (material or surface) treatment as the 16–25 branch — the classic
   look IS the correct classic-iOS presentation, not a degraded one.
4. No per-view `#available` sprawl: today the branch lives in exactly one file (the bar composite).
   The moment a SECOND glass surface family is adopted, hoist ONE Core helper
   (`.cleansiaGlass(_:in:)` view modifier encapsulating the branch + fallback) and route all glass
   through it. The natural moment is T-0376's AC3 pill harvest to `CleansiaCore` (the ≥2-call-sites
   rule) — the glass branch moves to Core with the shared pill.
5. Parity guard: Android has no Liquid Glass. The pill's translucency is an OWNER-DIRECTED deviation
   (ADR-0022 D3 amendment). Any further glass adoption that changes a surface Android renders opaque
   needs the same explicit owner sign-off, not a silent iOS-only change.

## Inventory (customer app chrome sweep)

| Surface | Current treatment | Glass candidate? | iOS 16–25 fallback | Ruling |
|---|---|---|---|---|
| **CustomerBottomBar pill** (`Features/Shell/CustomerBottomBar.swift`) | iOS 26: `GlassEffectContainer` + `.glassEffect(.regular.interactive(), in: r32 rect)`, stroke dropped; <26: `.ultraThinMaterial` + 1pt outlineVariant stroke | YES — the canonical floating-chrome surface; the owner's comparison target | current material+stroke (unchanged) | **ADOPTED NOW** (this phase) |
| **Book FAB** (same file) | iOS 26: `.glassEffect(.regular.tint(primary).interactive(), in: Circle())` inside the same container (merges with the pill — one glass group; the 4pt background ring dropped); <26: opaque primary circle + 4pt ring | YES — tinted interactive glass is the system prominent-action look; opaque disc read as a sticker on glass (screenshot-compared) | current opaque + ring (unchanged) | **ADOPTED NOW** (this phase) |
| **Partner `FloatingIslandBottomBar` port** (T-0376, not yet built) | n/a — stock `TabView` interim (ADR-0022 D4) | YES — same bar family; MUST reuse the customer pill's glass branch via the AC3 Core harvest | same as customer pill | **FOLLOW-UP → fold into T-0376** (add the glass branch to its AC3 harvest scope) |
| **AddressTopBar** (`Features/Home/HomeTab.swift:165`) | plain row INSIDE the Home `ScrollView` — scrolls away with content | NO — not floating chrome; glass on in-scroll content violates the guidance | n/a | **SKIP** — would need pinning first, which is an Android layout deviation |
| **Snackbar** (`Core/Snackbar/GlobalSnackbarHost.swift` — SHARED with partner) | floating pill, severity-coded OPAQUE palette (4 severities), shadow | Floating chrome, but NO — the severity color coding is the cross-platform parity signal; glass tint washes the 4-color semantics out and hurts text contrast on busy content; shared-Core change would force a partner re-verify | n/a | **SKIP** — the opaque severity palette is load-bearing |
| **BusyMascotOverlay card** (`Components/BusyMascotOverlay.swift`) | modal card (surface fill) over a 0.45 scrim | NO — a modal dialog-class card; Apple keeps dialogs on materials, not floating glass | n/a | **SKIP** |
| **Booking sheet header** (`Features/Booking/BookingSheetView.swift:172`) | plain row on sheet background | NO — sits on the sheet's own background, not above scrolling content | n/a | **SKIP** |
| **Booking sheet footer** (same file, `:223`) | pinned action bar, opaque `surface` fill above scrolling step content | MARGINAL — it IS pinned chrome, but Android renders it opaque surface (parity) and it hosts the primary CTA/SlideToConfirm where legibility beats translucency | `.thinMaterial` would be the fallback IF ever adopted | **FOLLOW-UP (owner call)** — only with an explicit owner directive like the pill got; do nothing by default. File is currently in the onboarding/profile dev's lane |
| **SlideToConfirm track/thumb** (`Core/Components/SlideToConfirm.swift`) | brand-primary track/thumb (two styles), custom control | NO — a control, not chrome; the solid primary track is the drag affordance + `SwipeToConfirmButton.kt` parity | n/a | **SKIP** |
| **CleansiaDialog** (`Core/Components/CleansiaDialog.swift`) | surface card over a 0.4 scrim | NO — dialog-class; same reasoning as BusyMascotOverlay | n/a | **SKIP** |
| **Splash** (`Features/Splash/SplashGateView.swift`) | full-screen brand gradient + mascot | NO — full-screen brand content, nothing floats | n/a | **SKIP** |
| **OrderDetail LiveProgressHero** (`Features/Orders/LiveProgressHero.swift`) | gradient content card + stroke, in-scroll | NO — a content card; glass never on content | n/a | **SKIP** |
| **Upsell carousel dots** (`Features/Home/UpsellCarousel.swift:42`) | 8pt capsules on the background, below the pager | NO — 8pt indicators are far below glass's legible size and sit on background, not over content | n/a | **SKIP** |

## Net result

Exactly ONE glass family exists (the bottom-bar composite), in ONE file, with ONE availability branch
per element. No Core helper was introduced this phase (anti-speculation: a helper for a single family
in a single file abstracts nothing) — the sanctioned hoist moment is T-0376 AC3. The only PM actions:
(1) fold "carry the iOS 26 glass branch into the Core pill harvest" into T-0376's AC3, and (2) note
the booking-footer question as an owner-decision item if the owner ever asks for more glass; every
other surface is a deliberate SKIP with the rationale above.

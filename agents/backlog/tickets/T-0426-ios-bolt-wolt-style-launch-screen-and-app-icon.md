---
id: T-0426
title: "iOS — Bolt/Wolt-style branded launch screen + app icon (both apps)"
status: proposed
size: S
owner: ios
created: 2026-07-16
updated: 2026-07-16
depends_on: []
blocks: []
stories: []
adrs: []
layers: [ios]
security_touching: false
priority: medium
manual_steps:
  - "owner: provide the app-icon artwork (1024px master per app) + the launch wordmark/logo + the brand hex (light+dark)"
sprint: 12
source: owner remark ("change load screen + icon like Bolt/Wolt/Foodora") + remarks-sweep (wf_064232d3)
---

> Owner wants the launch screen + app icon to match the Bolt/Wolt/Foodora style (bold full-bleed brand splash
> + a clean modern icon). **Small dev work, gated on artwork.**

## Current state
- The OS launch screen (`UILaunchScreen` in both `project.yml`) is **color-only** — a flat `SplashBackground`
  color with no logo (a deliberate workaround: `UIImageName` scaled/broke on the simulator).
- A real branded splash (mascot + "Cleansia" wordmark + gradient) exists only as an **in-app SwiftUI view**
  (customer `SplashGateView`); the partner `SplashGateView` is just a spinner.
- App icons are a single 1024² PNG per app in the modern single-size `AppIcon.appiconset`.

## Approach (two layers)
1. **OS launch screen:** replace the color-only `UILaunchScreen` with a **Launch Screen storyboard**
   (`UILaunchStoryboardName`) — a brand-color full-bleed background with a centered logo/wordmark. Use a
   storyboard (not raw `UIImageName`) to avoid the documented launch-image scaling bug. Files:
   `{CleansiaCustomer,CleansiaPartner}/project.yml`, a new `LaunchScreen.storyboard` + a logo imageset,
   `SplashBackground.colorset` (brand hex, light+dark).
2. **In-app splash continuity:** align both apps' `SplashGateView` to the same launch background (optional
   wordmark), and upgrade the partner splash from a bare `ProgressView` to match.
3. **App icon:** swap the 1024² masters in `Resources/Assets.xcassets/AppIcon.appiconset` (owner artwork).
4. Update the splash/mascot asset tests.

## Acceptance criteria
- [ ] **AC1** — both apps show a branded full-bleed launch screen (brand color + centered logo/wordmark) via
  a storyboard; no letterboxing; correct in light+dark.
- [ ] **AC2** — new app icons render on the home screen for both apps.
- [ ] **AC3** — the in-app `SplashGateView` matches the launch background (no color jump); partner splash
  upgraded from the spinner.
- [ ] **AC4** — both apps build on the iOS-16 floor; asset/splash tests green.

## Owner input required
Provide: the app-icon artwork (1024px per app), the launch logo/wordmark, and the brand hex (light+dark).
Dev work is small once these land.

## Status log
- 2026-07-16 — filed from the remarks-sweep; blocked on owner artwork.
- 2026-07-19 — owner will generate the artwork with OpenArt (Lottie for animation later, OpenArt first).
  Prompt pack delivered (below) using the live brand palette: primary #0284C7 (sky600), accent #38BDF8
  (sky400), tint #E0F2FE (sky100). Still blocked on the owner picking/generating the artwork.

## OpenArt prompt pack (2026-07-19)

Settings for ALL prompts: 1024×1024, flat vector / minimal style model if available; generate 4+
variants per prompt. iOS masks its own corners — the icon must be a FULL-BLEED SQUARE: no rounded
corners, no transparency, no drop shadows, no text.

**Prompt A — minimal glyph (recommended for the app icon):**
"Minimal flat vector app icon, a single stylized water droplet merging with a sparkle/shine
four-point star, soft geometric shapes, deep sky blue #0284C7 background, the droplet-sparkle glyph
in white with a light blue #38BDF8 inner accent, centered, generous padding, flat design, no
gradients except a subtle vertical shift from #0284C7 to #38BDF8, no text, no border, no rounded
corners, clean modern SaaS branding, dribbble style"

**Prompt B — bubble motif:**
"Flat vector logo mark for a home-cleaning service app, three overlapping soap bubbles forming an
upward arc suggesting freshness and motion, white and #E0F2FE bubbles with #38BDF8 outlines on a
solid #0284C7 square background, one small four-point sparkle highlight, minimal, geometric,
perfectly centered, no text, no shadow, flat modern app icon"

**Prompt C — C-lettermark (survives a rebrand poorly — only if the name stays):**
"Minimalist letter C logo formed by a swooshing cleaning-wipe motion trail ending in a small
sparkle, flat vector, white mark centered on a #0284C7 to #38BDF8 diagonal gradient square, bold
rounded terminals, no text besides the C shape, no border, modern fintech-grade simplicity"

**Negative prompt (all):** "text, words, letters (except prompt C's mark), photorealistic, 3D render,
skeuomorphic, drop shadow, rounded corners, border, frame, watermark, busy details, hands, people"

Wordmark: generate separately or set the existing Poppins-Bold "Cleansia" text in white — do NOT ask
the generator for text (it garbles type). Launch screen = the chosen glyph centered on the
#0284C7→#38BDF8 gradient (matches the shipped SplashBrandingView).

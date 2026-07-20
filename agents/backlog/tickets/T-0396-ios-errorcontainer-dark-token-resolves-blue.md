---
id: T-0396
title: "iOS design token — CleansiaColors.errorContainer resolves to blue (Palette.sky800) in DARK mode, so any destructive 'container' surface is blue-on-dark"
status: done
size: S
owner: architect
created: 2026-07-08
updated: 2026-07-19
depends_on: []
blocks: []
stories: []
adrs: []
layers: [ios]
security_touching: false
priority: low
manual_steps: []
sprint: 12
source: phase/ios-fix2 fix-round-5 D-profile+plus+dialog review (minor, latent token bug)
---

> **Found by fix-round 5 (delete-account red treatment).** `CleansiaColors.errorContainer` is defined
> `Color.dynamic(light: errorBg, dark: Palette.sky800)` — the destructive **container** token resolves to a
> **blue** (`sky800`) in dark mode. Any destructive surface that uses `errorContainer` as its fill in dark mode
> renders blue instead of an error-family tint. Fix-round 5's delete/dialog work correctly routed AROUND it
> (using `.error.opacity(...)` for the red container), but the token itself is a latent trap for the next author.

## Context
- `CleansiaCore/Sources/CleansiaCore/DesignSystem/CleansiaColors.swift` (~line 32): `errorContainer` dark ⇒
  `Palette.sky800`. `sky800` is a blue in the brand palette.
- Symptom would appear on any destructive "container" surface (delete confirmations, error banners) in dark mode
  that fills with `errorContainer`.

## Acceptance criteria
- [ ] **AC1** — `errorContainer` dark resolves to an **error-family** tint (a dark, low-chroma red container),
  not `sky800`; OR, if the blue is intentional for some non-destructive use, the token is renamed/split so the
  destructive-container use has a correct red dark value. Record the decision (architect owns the palette).
- [ ] **AC2** — audit the callers of `errorContainer` in both apps; any destructive surface reads a red dark
  value after the fix; no unintended visual change to non-destructive callers.
- [ ] **AC3** — a snapshot/inspection check (or a documented manual dark-mode check) confirms the destructive
  container is red in dark mode.

## Out of scope
- The fix-round-5 delete/dialog surfaces (already correct — they use `.error.opacity`).

## Status log
- 2026-07-08 — filed `proposed` by pm from the fix-round-5 D-review. Low priority (no shipped surface is wrong
  today — everything routes around it — but it's a latent design-system bug worth correcting at the token).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
- 2026-07-19 — ios: implemented. Android dark schemes don't override the error-container slots, so the
  device renders the Material3 baseline — mirrored verbatim: dark `errorContainer` = `#8C1D18` (error30,
  replaces the interim Tailwind red-900 `#7F1D1D` that had superseded the original blue `sky800`), dark
  `onErrorContainer` = `#F9DEDC` (error90, replaces `darkError` red-300). All 4 `errorContainer` call sites
  are destructive surfaces (partner contract chip + invoice badge, customer dispute banner + delete-account
  halo) — none relied on blue. Harvest: baseline-mirror rule folded into `patterns-mobile.md` colors row.
  Follow-up candidates (out of scope here): fix-round-5 `.error.opacity(...)` container workarounds could
  now adopt the token; light `errorContainer` (`#FEE2E2`) also deviates from the Android-effective baseline
  (`#F9DEDC`) — minor, pale-pink vs pale-pink.

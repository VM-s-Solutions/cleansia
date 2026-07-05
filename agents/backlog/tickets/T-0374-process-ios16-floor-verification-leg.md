---
id: T-0374
title: "Process — iOS 16 floor verification leg: every iOS slice must smoke on the iOS 16.4 simulator; + the Gate-DP §G hardening (asset-counterpart sub-check + one-time app-chrome item)"
status: in_progress
size: S
owner: qa
created: 2026-07-03
updated: 2026-07-03
depends_on: []
blocks: []
stories: []
adrs: [ADR-0014, ADR-0016, ADR-0018]
layers: [architect, qa, docs]
security_touching: false
priority: high
manual_steps: []
sprint: 12
source: phase/ios-fix1 on-device shakeout diagnosis (2026-07-02, cross-cluster NOTES)
---

> **Why this exists:** ALL of the crash/⚠️/island issues in phase/ios-fix1 were INVISIBLE on the
> latest-runtime simulator — iOS 17+ reworked the navigation authority (masking the `comparisonTypeMismatch`
> crash and the yellow-⚠️ missing-destination placeholder) and the modern system tab-bar styling masked the
> never-ported island bar. The project's declared floor is iOS 16 (ADR-0014, iPhone 8/X-class reach), yet no
> gate ever exercised it. The iOS 16.4 runtime is now installed locally (devices listed under
> `-- iOS 16.4 --` in `xcrun simctl list devices`).

## Context
Sixteen owner-reported on-device issues; the 4-cluster diagnosis grounded three of the four worst (the Plus
crash, the ⚠️ pushes, the island bar) as **floor-only** defects, and the brand cluster as a class of gate
misses the review checklist structurally could not see (its citation unit is the `.kt` screen file, so
`res/` raster assets and app-level packaging — AppIcon, UILaunchScreen — were never owned by any screen
ticket).

## Acceptance criteria
- [ ] **AC1 (quality-gate leg)** — `agents/process/quality-gates.md` gains an iOS-16-floor verification leg:
  every iOS ticket's evidence must include an **iOS 16.4-simulator smoke of the touched surfaces** (launch +
  navigate every push the diff introduces/modifies + the changed screens rendered), in addition to the
  latest-runtime test suite. A latest-only run is an incomplete gate for any `layers: [ios]` ticket.
- [x] **AC2 (Gate-DP §G hardening — the architect is folding this in)** —
  `agents/backlog/ios-app-review-checklist.md` §G gains the two lines from the diagnosis NOTES:
  (a) an **AR-DP-1 sub-check**: "every `drawable`/`raw` asset referenced by the cited screen has an iOS
  asset-catalog counterpart (SF-symbol substitution allowed only for Material ICONS, never for brand raster
  art)"; (b) a **one-time per-app app-chrome item**: AppIcon + launch screen + splash verified per app.
  *(DELIVERED in `987f85f0` as AR-DP-1a + AR-DP-4 — grep-verified present in the checklist.)*
- [x] **AC3 (applies to this phase)** — Every phase/ios-fix1 slice (T-0368, T-0369, T-0370's iOS surface,
  T-0371, T-0372, T-0373) carries iOS 16.4 smoke evidence in its ticket before the phase exits; the phase's
  Definition-of-Done references this leg. *(EXECUTED — see the status log; the F-1 catch is the leg's
  proof-of-value.)*
- [ ] **AC4 (recorded rationale)** — The gate text records WHY (one paragraph: nav-authority rework +
  system-styling masking), so the leg isn't later "optimized away" as redundant.

## Out of scope
- Real-device CI (macOS runners can't attach physical devices) — the leg is simulator-based; owner device
  passes remain ad-hoc.
- Adding an iOS 16.4 destination to `ios-ci` — desirable but a separate CI-cost decision (runtime download
  on hosted runners); note it in the gate text as a candidate follow-up, do not block on it.

## Implementation notes
- Owner of the AC2 checklist edit is the **architect** (it amends the ADR-0016/0018 review instruments);
  the AC1 gate edit is qa+docs. One combined PR-able doc change; no code.
- Keep the smoke checklist SHORT and surface-scoped (what the diff touched), not a full manual regression —
  the point is the floor runtime, not more steps.

## Status log
- 2026-07-03 — filed `proposed` by pm from the phase/ios-fix1 diagnosis cross-cluster NOTES (every
  floor-only defect was invisible on the latest-runtime sim; the brand misses were structural to the gate's
  citation unit). No-decision note: process/instrumentation change composing accepted ADRs (0014/0016/0018)
  — no new architecture decision, so no panel; the architect's §G fold-in is already in flight.
- 2026-07-03 — pm: phase-close reconciliation → **in_progress** (partially delivered; deliberately NOT
  closed). **EXECUTED throughout phase/ios-fix1 (AC3 done):** every slice carries iOS 16.4 evidence in its
  ticket — A/B: the 16.4 boot-install-launch smoke with 0 NavigationAuthority/comparisonTypeMismatch hits;
  C: the install-seam + offset-less-date tests run against the floor-relevant decode chain; E: the 16.4
  visual smoke (splash/auth/icons); F: the Customer suite re-run green ON the 16.4 destination. The leg
  already PAID FOR ITSELF: the **F-1 catch** (`BrandGradientTests` 18 failures on the 16.4 runtime ONLY —
  green on iPhone 17; `UIColor(Color)` flattens dynamic providers pre-iOS-17) is exactly the defect class
  the leg exists for. **AC2 done:** the architect's Gate-DP §G hardening landed in `987f85f0` (AR-DP-1a +
  AR-DP-4). **OPEN (AC1 + AC4):** the DURABLE codification of the floor leg + its WHY paragraph into
  `agents/process/quality-gates.md` (grep-verified still absent) so the leg survives beyond this phase's
  discipline — qa+docs, doc-only, no code. Keep priority high; dispatch next sprint window.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

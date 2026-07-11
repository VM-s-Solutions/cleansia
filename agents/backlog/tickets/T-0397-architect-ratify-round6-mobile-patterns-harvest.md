---
id: T-0397
title: "Architect — ratify the fix-round-6 patterns-mobile harvest (full-bleed header-to-top idiom + self-sizing short-entry-sheet detent)"
status: proposed
size: S
owner: architect
created: 2026-07-08
updated: 2026-07-08
depends_on: []
blocks: []
stories: []
adrs: []
layers: [architect, ios, docs]
security_touching: false
priority: low
manual_steps: []
sprint: 12
source: phase/ios-fix2 fix-round-6 reviews (B/C/D) — new "one way to do X" catalog rows need Architect sign-off
---

> **Flagged by three fix-round-6 reviewers.** Fix-round 6 harvested two NEW canonical iOS rows into
> `agents/knowledge/patterns-mobile.md` that redefine "the one way to do X" — which per the reviewer charter
> (Gate 5) is an Architect ratification, not an inline reviewer approval. The rows are useful and were written
> from working code, but they should be Architect-owned canon, not dev-landed.

## Context
Two catalog additions (plus a factual mascot-row correction that is fine to keep as-is):
1. **Full-bleed colored header to the top edge, inside a ScrollView** — `GeometryReader { proxy in … }
   .ignoresSafeArea(.container, edges: .top)` with `proxy.safeAreaInsets.top` threaded as the header's INTERNAL
   top padding, so the gradient fills from `y=0` while content stays below the status bar. Names the failed
   round-5 child-`.ignoresSafeArea` approach a defect. (Used by ProfileTab hero + SubscribePlusScreen hero.)
   NOTE: this idiom was **verified on-simulator in the fix-round-6 fold** (Profile + Plus, iPhone 17 + iPhone 14)
   — the catalog row reflects the approach that actually renders correctly.
2. **Self-sizing short input/entry sheet detent** — a short entry sheet (promo/referral `CodeSheetShell`) must
   NOT use a fixed `.medium` detent; instead self-size to content via `.fixedSize(…, vertical: true)` + a
   `GeometryReader` `PreferenceKey` height + `.presentationDetents([.height(measured)])` (16.0-safe), no trailing
   `Spacer()`.

## Acceptance criteria
- [ ] **AC1** — Architect reviews both rows for correctness + canonical phrasing; either ratifies them in
  `patterns-mobile.md` (as the sanctioned mobile idiom) or revises/relocates them (e.g. into an ADR if they
  carry a real trade-off). The header idiom's on-sim verification (fix-round-6 fold) is the evidence base.
- [ ] **AC2** — if ratified, add a `check-consistency.mjs` rule where checkable (e.g. flag a child
  `LinearGradient().ignoresSafeArea(edges:.top)` inside a ScrollView as the known-broken pattern), or note why
  it is not mechanically checkable.
- [ ] **AC3** — the mascot-row factual correction (`shouldPinFinalFrameOnUpdate` + loop-count-ignored +
  pin-on-every-update) is confirmed accurate against the shipped `AnimatedMascotView` and kept.

## Out of scope
- Re-implementing the header/sheet fixes (shipped in fix-round 6).

## Status log
- 2026-07-08 — filed `proposed` by pm from the fix-round-6 B/C/D reviews. Low priority (the code shipped and is
  verified; this is catalog-governance to keep "the one way" Architect-owned).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->

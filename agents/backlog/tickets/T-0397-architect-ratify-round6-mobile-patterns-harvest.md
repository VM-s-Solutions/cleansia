---
id: T-0397
title: "Architect — ratify the fix-round-6 patterns-mobile harvest (full-bleed header-to-top idiom + self-sizing short-entry-sheet detent)"
status: done
size: S
owner: architect
created: 2026-07-08
updated: 2026-07-19
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
- 2026-07-19 — **done** by architect (lead ruling, see Review): both rows RATIFIED (header idiom
  verified at 3 call sites and extended with the fix-round-8 `.animation(nil, value: topInset)` settle
  pin; sheet detent verified at `CodeSheetShell` + `PackageDetailsSheet`); AC2 recorded as
  not-mechanically-checkable (modifier attachment is structural, T-0417 E9 precedent); mascot
  correction (AC3) confirmed against `AnimatedMascotView` + its playback tests and kept.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->

**Architect ratification verdicts (lead-adjudicated, 2026-07-19):**

- **AC1 / row 1 — full-bleed header idiom: RATIFIED, with one factual addendum.** Author case: the row
  was written from working code and names both failed alternatives as defects with the on-sim evidence
  (round-6 fold, iPhone 17/iOS 26 + iPhone 14/iOS 16.4). Challenger case (steelmanned): a layout idiom
  with two documented failure modes and a subtle proxy-vs-modifier placement rule smells like it
  carries a real trade-off — should it be an ADR, not a catalog row? Ruling: no trade-off survives —
  the two alternatives are not competing options with prices, they are empirically BROKEN renderings
  (collapsed inset / no upward bleed); a row that names the one working shape plus its defect forms is
  exactly what the catalog is for. Verified against all THREE call sites (the ticket named two): customer
  `ProfileTab.swift:23-52` (+ `HeroGradient` `.padding(.top, Spacing.m + topInset)` inside the gradient
  background), `SubscribePlusScreen.swift:34-49` (+ `HeroBlock:167`), partner `ProfileHubContent.swift:22-35`
  — each is GeometryReader-wrapped with `.ignoresSafeArea(.container, edges: .top)` on the INNER
  ScrollView and the threaded internal top pad, matching the row's prescription exactly. One
  code-vs-row gap found and folded into the row: the fix-round-8 refinement — `proxy.safeAreaInsets.top`
  settles 0→real on first layout, so an animatable header must pin `.animation(nil, value: topInset)`
  (shipped in `SubscribePlusScreen` HeroBlock:183-187; the row predated it).
- **AC1 / row 2 — self-sizing short-entry-sheet detent: RATIFIED as-is.** Verified against
  `CodeSheetShell.swift:26-37`: `.fixedSize(horizontal: false, vertical: true)` + a background
  `GeometryReader` publishing `CodeSheetHeightKey` (`PreferenceKey`) + `.onPreferenceChange` +
  `.presentationDetents([.height(contentHeight)])`; no trailing `Spacer()`. Second adopter already
  exists (`PackageDetailsSheet.swift:28`) — the ≥2-call-sites bar is met. `.height(_:)` detents are
  iOS 16.0 API, so the "16.0-safe" claim holds. Ratification signatures added to both rows in
  `patterns-mobile.md`.
- **AC2 — not mechanically checkable at useful precision; no tool rule filed.** The known-broken form
  (a child `LinearGradient().ignoresSafeArea(edges:.top)` inside a ScrollView) differs from the
  sanctioned form only by WHICH node carries the modifier — a structural property a lexical scanner
  cannot resolve (same reasoning as the T-0417 E9 warn-only precedent; a regex on
  `ignoresSafeArea` + `ScrollView` co-occurrence would false-positive every correct call site, which
  attaches both in the same file). The catalog row's named defect forms + Gate-DP screen review carry
  the enforcement.
- **AC3 — mascot correction CONFIRMED and kept.** Row verified against `AnimatedMascotView.swift`:
  `AnimatedMascotPlayback.shouldPinFinalFrameOnUpdate(loop:hasCompletedFrame:superseded:)` (:27),
  `pinnedFinalFrame`/`completedGeneration` (:89-90) pinned on completion (:168-169) and re-asserted on
  EVERY `updateUIView` gated on non-superseded (:174-177); `AnimatedMascotPlaybackTests` pin the truth
  table. The loop-count-ignored claim and the pin-on-every-update (not once) claim both match the code.

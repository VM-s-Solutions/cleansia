---
id: T-0340
title: "Order-detail parity nits: iOS checklist stable-id keying + Android status-label casing convergence"
status: done
size: S
owner: pm
created: 2026-06-27
updated: 2026-07-17
depends_on: [T-0307]
blocks: []
stories: []
adrs: [0013, 0018]
layers: [ios, android]
security_touching: false
sprint: 12
source: T-0307 Slice E reviewer (Findings 2 + 3, dispositioned as deferred parity nits)
---

> Two small, non-blocking parity nits surfaced by the T-0307 Slice E review. Both dormant today; filed so they
> aren't lost. The PM/architect can fold these into a future Android-parity pass.

## Nit 1 (iOS) — CleaningChecklist item keys should be stable backend ids, not positional index

`CleaningChecklistView` keys service/package checklist items by **positional index** (`"service:<index>:<name>"`)
because `OrderDetail.services`/`packages` are `[String]` (names only — the Slice-C `OrderItem → OrderDetail`
mapping dropped the backend ids). The checked-set persists to UserDefaults under those keys, so a ticked item
survives a refetch **only while the services/packages list keeps the same order** — a reorder silently orphans
ticks. Android (`CleaningChecklist.kt:60-70`) keys by the stable backend ids `service.id`/`package.id`, which is
order-independent. **Dormant today** (single-cleaner, server returns a stable order), so it ships as index-keyed
with a limitation comment.

**Fix:** thread the stable service/package ids into `OrderDetail` (a richer service/package item type, or a
parallel id list) and key the checklist by them — matching Android. Touches `OrderDetail.swift` (the model +
the Slice-C mapping), `CleaningChecklistView.swift` (the keys), and possibly `ScopeCard` (renders names).
Extras already key correctly by slug (`"extra:<slug>"`).

## Nit 2 (Android) — `labelForStatusName` code contradicts its own comment

Android `StatusTimeline.kt:160-163` does a camel-split + uppercase-first with **no lowercasing**, so it renders
**"On The Way" / "In Progress"** — but its own comment claims "On the way". iOS `OrderStatusLabel.prettify`
implements the **commented intent** (lowercased → "On the way" / "In progress"), which is the better UX and what
Android *meant*. Per ADR-0013 ("mirror the code, not the doc") a strict reading would have iOS match Android's
*output*; instead, since Android's code is the bug (it contradicts its documented intent), the chosen disposition
is **converge on the correct form**: iOS keeps "On the way"; **Android is fixed** to lowercase so both platforms
render the intended label. The iOS form is pinned by `OrderStatusLabelTests`.

**Fix (Android):** make `labelForStatusName` lowercase the camel-boundary char (or otherwise produce
"On the way"/"In progress"), converging with iOS. Update the Android comment-vs-code mismatch.

## Nit 3 (iOS, trivial) — stale placeholder-preview literal

`PlaceholderTabView.swift:26` (a `#if DEBUG` PreviewProvider) still hardcodes `"Orders — coming in T-0307"`,
though the Orders tab now ships the real `OrdersRootView`. Preview-only, pre-existing (phase-2 commit), harmless,
but a dangling ticket id in source. Sweep it (and the sibling Invoices/Profile preview placeholders) when those
tabs land (T-0309/T-0310) or in a one-line cleanup here.

## Done when
- [ ] iOS checklist items key by stable backend ids (order-independent persistence).
- [ ] Android `labelForStatusName` renders "On the way"/"In progress" (converged with iOS).
- [ ] `PlaceholderTabView` DEBUG-preview literals drop the stale ticket ids.

## Status log
- 2026-06-27 — filed from the T-0307 Slice E review (Findings 2 + 3). Both dormant; deferred to keep the final
  T-0307 slice scoped. iOS Slice E ships index-keyed (limitation noted in-code) + the iOS "On the way" form.

## Status log
- 2026-07-17 — all 3 nits shipped on `feature/i18n-cluster-3`: stable-id checklist keying (OrderDetailService
  + id on OrderDetailPackage, threaded from the wire dtos; ticks now reorder-proof, old index-keyed ticks
  orphan once — dormant), Android `labelForStatusName` lowercases camel-boundary words (converged on
  "On the way"/"In progress", pinned by the new `StatusTimelineLabelTest` mirroring iOS's
  `OrderStatusLabelTests`), and the stale "coming in T-0307" preview literal swept. Partner iOS
  BUILD SUCCEEDED; Android partner tests green.

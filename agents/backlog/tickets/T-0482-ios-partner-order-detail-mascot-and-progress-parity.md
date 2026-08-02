---
id: T-0482
title: iOS partner order detail has no mascot puck and no job-progress affordance; Android does
status: draft
size: M
owner: ios
created: 2026-08-02
updated: 2026-08-02
depends_on: [T-0489]
blocks: []
stories: []
adrs: [0018]
layers: [ios]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Owner remark #4, first half (2026-08-02):** *"iOS order detail does not match Android — no progress
bars, no mascot art. Replicate."*

### Ground truth — PM-verified on `master` at `0e4ede1b`, and it relocates the defect

**The remark is false for the customer app and true for the partner app.**

- **Customer iOS already has all three.** `OrderDetailContent.swift:21` swaps in
  `LiveProgressHero(order:)` for active orders, and `LiveProgressHero.swift` carries a mascot overlay
  (`:88-94`, `AnimatedMascotView(.cleaningInProgress …)` / `.welcoming`), a real progress bar
  (`:73`, `ProgressView(value: fraction)` inside a 30-second `TimelineView`) and a `StepIndicator`
  (`:121`). **Nothing to replicate.**
- **Partner iOS has none of it.** `CleansiaPartner/Sources/Features/Orders/OrderDetailView.swift` is
  143 lines: `SnapSheet` + map backdrop + `OrderDetailContent`. The only `ProgressView` is the
  loading spinner at `:54`. A grep across the whole partner Orders feature returns **`MascotEmptyState`
  on the orders LIST only** (`OrdersListComponents.swift:214`) — no mascot on the detail.

**What Android's partner detail has that iOS's does not** (`partner-app/.../orders/OrderDetailScreen.kt`,
735 lines):

| Element | Android | iOS |
|---|---|---|
| `FloatingMascot` puck | `:321-324`, documented `:321-323` as *"Foodora-style mascot puck: floats over the sheet edge… Animated WebP for InProgress, static PNG others"*, anchored to the sheet's top edge so half sits over the map | **absent** |
| Job progress affordance | `:528` — *"progress bar with no whitespace between them"* inside the sheet content | **absent from the detail** (the checklist has its own `ProgressView` at `CleaningChecklistView.swift:94` — a *different* thing: checklist completion, not job progress) |

**The mascot is not decoration on this screen.** On Android it is the element that tells a cleaner at
a glance which of Confirmed / OnTheWay / InProgress they are in, half-overlapping the map/sheet seam.

## Acceptance criteria

- [ ] **AC1 — the mascot puck exists on the iOS partner order detail, anchored to the sheet edge.**
      Given an order in each of Confirmed, OnTheWay, InProgress, When the detail renders, Then a
      mascot sits at the map/sheet seam in the same relationship Android's does (roughly half over
      each). The **animated-vs-static** rule is matched: animated for InProgress, static otherwise,
      per `OrderDetailScreen.kt:321-323`. Evidence: three screenshots, one per status, plus one
      showing the puck's behaviour while the sheet is **dragged** — the seam moves, so the anchor
      must follow.
- [ ] **AC2 — the assets EXIST before the view is written, and are not invented.** State which
      partner mascot assets are already in `CleansiaPartner`'s asset catalog and which Android
      drawables have no iOS counterpart. **If an asset is missing, STOP and report it** — do not
      substitute an SF Symbol and call it parity. Evidence: the asset inventory, both sides.
- [ ] **AC3 — the job-progress affordance is PORTED or REFUSED with a reason.** Read
      `OrderDetailScreen.kt:520-540` and state what the Android progress element actually measures.
      Then either port it, or state why it should not exist on iOS. **A silent omission fails this
      AC.** Evidence: the diff, or the argued refusal in `## Review`.
- [ ] **AC4 — the checklist progress bar is NOT mistaken for the job progress bar.**
      `CleaningChecklistView.swift:81-94` already renders `ProgressView(value: doneCount/allItems)`.
      The verdict states explicitly that this is checklist completion and is a different affordance —
      because an implementer who finds it will reasonably conclude AC3 is already satisfied.
- [ ] **AC5 — the SnapSheet is not restructured.** `SnapSheet` is a `CleansiaCore` component
      (`Components/SnapSheet.swift`) shared with **T-0489**, which is changing how its anchors are
      reached. This ticket adds an **overlay**; it does not modify `SnapSheet`. If the anchor cannot
      be expressed without touching Core, **stop and report** rather than editing a file another
      ticket owns. Evidence: `git diff --stat` shows no `CleansiaCore/**/SnapSheet.swift` change.
- [ ] **AC6 — dark mode.** All three status screenshots, dark. Evidence: three more screenshots.
- [ ] **AC7 (Gate 0.5)** — `xcodebuild build test` for `CleansiaPartner` on the **16.4 floor** +
      `CleansiaCore` from the package dir, SwiftFormat `--lint` 0.60.1 / SwiftLint `--strict` 0.65.0,
      with an honest statement of whether the app-scheme tests compiled and ran. Any assertion added
      (e.g. the status→asset mapping) is **mutation-proved and named**. The screenshots are leg-3
      evidence, not a mutation target.

## Out of scope

- **The customer app on either platform.** It already has all three elements — verified above.
- **The full parity gap list.** That is **T-0481**, running in parallel. This ticket fixes the one
  gap that was confirmed before the audit started; the audit will find the rest.
- **The map/sheet anchoring behaviour** — **T-0489**. Adjacent geometry, different ticket. Coordinate
  ordering (see `## Implementation notes`).
- **Android.** It is the reference here, not the subject.

## Implementation notes

**No panel.** The design decision was made on Android and is being ported; ADR-0018 parity is the
argument *for* the port, not a question about it. If AC3's read finds the Android progress element to
be substantively new behaviour rather than a display of existing state, **that** needs a panel — say
so and stop.

**⚠️ Sequencing against T-0489, and getting it backwards costs a rebase:** T-0489 changes how the
partner sheet reaches `.mapFocus`; this ticket anchors a puck **to the sheet's moving top edge**. They
are geometrically coupled on the same screen. **Run T-0489 first**, then anchor against the final
anchor set. Recorded on both tickets.

**Do not open `src/cleansia_ios/**/Info.plist` or `**/project.yml`.**

**Before starting:** `src/cleansia_ios/scripts/generate-api-clients.sh` + `xcodegen generate` in both
app dirs (**T-0474**). A new asset or a new Swift file that is not in `project.pbxproj` is silently
absent from the build — that exact failure has cost the owner a broken build three times.

## Status log
- 2026-08-02 — **draft (created by pm from the owner's remark #4, first half).** **The remark was
  corrected before ticketing**: the customer iOS order detail already carries the progress bar, the
  mascot overlay and the step indicator, all PM-verified at file:line. The confirmed gap is the
  **partner** app's `FloatingMascot` puck and its in-sheet progress element. Filed against the partner
  app only. The remaining "what else is missed" is **T-0481** and is not folded in here.

## Review

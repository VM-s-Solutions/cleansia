---
id: T-0484
title: DESIGN-FIRST — HTML concept for a customer order-detail redesign at partner-app quality
status: draft
size: M
owner: analyst
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: []
stories: []
adrs: [0018, 0022]
layers: [analyst, architect]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Owner remark #5 (2026-08-02), verbatim:** *"I find it ridiculous that the customer gets a poor
design for order detail while partner has it insanely good."* The owner wants an **HTML concept** for
a customer order-detail redesign at partner-app quality, adapted to customer needs (their actions +
order tracking), for **both** mobile apps.

> **⛔ THIS TICKET WRITES NO SWIFT AND NO KOTLIN.** It produces an HTML concept and stops at an owner
> decision point. **No implementation ticket exists behind it and none will be written until the
> owner approves a concept.** Inventing acceptance criteria for an unapproved design is how a
> redesign becomes three rewrites.

### Ground truth — what "insanely good" and "poor" actually are, PM-verified on `master` at `0e4ede1b`

**Partner order detail — the thing the owner likes:**

| | Android `partner-app/.../orders/OrderDetailScreen.kt` (735 lines) | iOS `CleansiaPartner/.../Orders/OrderDetailView.swift` (143) |
|---|---|---|
| Topology | Full-bleed **Mapbox** backdrop + `BottomSheetScaffold` (`:268`), documented at `:80` as *"v2 layout"* | `SnapSheet` (`CleansiaCore/Components/SnapSheet.swift`) over a `MapProvider` full-bleed map |
| Sheet | peek at **75%** of screen (`:255`), drag handle is a custom `OrderDetailCompactHeader` | three anchors — `.mapFocus` 0.30 / `.peek` 0.75 / `.expanded` 0.95 |
| Ornament | `FloatingMascot` puck at the sheet seam (`:321-324`) | **absent** — that is **T-0482** |
| Footer | `StickyActionFooter` (`:662`) | in-sheet actions |

**Customer order detail — the thing the owner calls poor:** `OrderDetailContent.swift:17-66` (iOS) /
`OrderDetailScreen.kt:563-700` (Android) is a **single vertical `ScrollView` of stacked cards** —
hero, address, cleaning details, services, packages, instructions, photos, cleaners, timeline, review,
receipt. No map, no sheet, no spatial hierarchy. **It is not badly built; it is a list.** That is the
gap, stated precisely so the concept solves the right problem.

**And it is not empty — the customer screen already has real substance the concept must keep:**
`LiveProgressHero.swift` gives active orders a mascot, a live `ProgressView` on a 30-second
`TimelineView`, and a 4-step `StepIndicator`. **A redesign that loses those is a regression**, and the
owner's own remark #4 shows they are the elements he values.

### The three things that make this a decision and not a mockup

1. **The customer has no map today, and giving them one is not free.** The partner map exists because
   a cleaner must *travel* to an address. A customer looking at their own home does not. The
   defensible customer analogue is **tracking the cleaner** — and that requires a **live cleaner
   position** the platform does not currently expose to customers. **The concept must say whether it
   assumes that data exists.** If it does, that is a backend + privacy epic, not a redesign.
2. **The actions are different.** Partner: take / start / on-the-way / complete / cash-confirm /
   checklist / photos. Customer: cancel, report issue, book again, make recurring, confirm recurring,
   review, receipt (`OrderDetailFooterActions`, `OrderDetailView.swift:236-275`). The sheet topology
   was designed around a *task list*; the customer has a *status* plus a *few* actions.
3. **ADR-0022 already ruled once on this class of topology** (the stock `TabView` supersede). A
   customer sheet-over-map is the same kind of shell decision and needs the same kind of record.

## Acceptance criteria

- [ ] **AC1 — the concept is HTML the owner can open in a browser, at `agents/archive/2026-08/backlog/attachments/`.**
      One self-contained file per concept, no build step, no CDN dependency (it must render offline).
      It renders at a **phone viewport** and is legible at 390×844. Evidence: the file paths.
- [ ] **AC2 — TWO or THREE concepts, not one, and they must genuinely differ in topology.** At least
      one that adopts the partner's map+sheet, and at least one that does **not** (e.g. a
      hero-led scroll with a pinned live-status header). A single concept is a proposal, not a
      choice. Evidence: the concepts, with a one-paragraph rationale each.
- [ ] **AC3 — every concept covers ALL SIX order statuses.** New/Pending, Confirmed, OnTheWay,
      InProgress, Completed, Cancelled. The screen changes shape across them (footer actions appear
      and disappear — see `OrderDetailFooterActions`), and a concept that only draws InProgress has
      designed the easy 20%. Evidence: six states rendered per concept.
- [ ] **AC4 — the data question from `## Context` item 1 is ANSWERED per concept, in writing.** For
      each concept: does it require data the platform does not have today (live cleaner position,
      ETA to door, route polyline)? If yes, say so **on the concept itself**, in the owner-facing
      copy, so approving a picture does not silently approve an epic. Evidence: the stated
      dependency, or "uses only data the order detail already loads".
- [ ] **AC5 — the existing live-progress elements survive, or their removal is argued.** Mascot,
      `ProgressView`, `StepIndicator`. Evidence: named per concept.
- [ ] **AC6 — both platforms are addressed by ONE concept each, not two.** The concept states which
      elements are platform-native (the sheet mechanism differs: M3 `BottomSheetScaffold` vs
      `CleansiaCore.SnapSheet`) and which are shared. Evidence: the platform note per concept.
- [ ] **AC7 — an honest implementation estimate per concept**, in `S`/`M`/`L` **per platform**, with
      the named files each would rewrite. **A concept whose estimate is `L` on either platform must
      say so loudly** — it is a multi-ticket epic, and the owner is choosing a budget as much as a
      picture. Evidence: the estimate table.
- [ ] **AC8 — the panel's rejected directions are recorded.** Per `deliberation.md`, the alternatives
      and why-nots stay in the artifact. Evidence: the `## Challenge` / `## Defense` / `## Verdict`
      trail in the analyst's living doc.
- [ ] **AC9 (Gate 0.5 leg 3)** — the concept states what it did **not** design: which states, which
      edge cases (no cleaner assigned, no photos, cancelled-with-fee), and every assumption about
      data it did not verify against the DTO.

## Out of scope

- **Any Swift or Kotlin.** Explicitly. `git diff --stat -- src/` must be **empty** for this ticket.
- **Any implementation ticket.** None is written until the owner picks a concept. The PM will file
  them from the approved concept, sized against AC7.
- **The partner order detail.** It is the reference, not the subject. (Its own gaps are T-0482 /
  T-0489.)
- **Web customer order detail.** Not named.
- **Building the live-cleaner-position feature.** AC4 *names* the dependency; it does not scope it.

## Implementation notes

**This is a story panel, and it is the reason the ticket is `analyst`-owned:** author + 2–3
challengers + lead per `process/deliberation.md`, with the `architect` sitting on the topology
question (AC6, and ADR-0022's precedent). The author drafts the concepts; the challengers attack them
on the three points in `## Context` — *"the customer has no reason to look at a map"*, *"this needs
data we do not have"*, *"this is an L disguised as a redesign"*. The lead adjudicates and the
analyst's living doc (`agents/analysts/<domain>.md`) is updated in the same step.

**⚠️ DECISION POINT after this ticket, and it is the owner's.** The output goes to the owner with
AC7's estimates. **Only then** does the PM file implementation tickets. This is written into the
sprint plan as a hard stop, not a handover.

**Read first:** `agents/knowledge/patterns-mobile.md`, `SnapSheet.swift` in full,
`partner-app/.../orders/OrderDetailScreen.kt:225-340` (the map+sheet composition), and both customer
`OrderDetailContent` files. **Do not read `src/cleansia_ios/**/Info.plist` or `**/project.yml`.**

## Status log
- 2026-08-02 — **draft (created by pm from the owner's remark #5).** Both topologies PM-verified at
  file:line before ticketing, including the exact sheet fractions on each platform and the customer
  screen's existing live-progress elements — because *"the customer screen is poor"* is true about the
  **topology** and false about the **hero**, and a concept written from the remark alone would delete
  the good part. Filed **design-first with an explicit owner decision point and no implementation
  ticket behind it**, per the owner's instruction.

## Review

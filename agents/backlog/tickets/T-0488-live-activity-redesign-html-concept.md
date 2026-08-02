---
id: T-0488
title: DESIGN-FIRST — HTML concept for the iOS Live Activity redesign (lock screen + all three Dynamic Island states)
status: draft
size: M
owner: analyst
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: []
stories: []
adrs: [0025]
layers: [analyst, architect]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Owner remark #7 (2026-08-02):** *"Redesign iOS Live Activities — wants HTML concepts first."*

> **⛔ THIS TICKET WRITES NO SWIFT.** It produces HTML concepts and stops at an owner decision point.
> **No implementation ticket exists behind it and none will be written until the owner approves a
> concept.**

### Ground truth — what exists today, PM-verified on `master` at `0e4ede1b`

`CleansiaCustomer/LiveActivity/CleansiaLiveActivity.swift`, **281 lines**, one widget
(`CleanOrderLiveActivity`, `:201`) in a `WidgetBundle` (`:20`):

| Surface | Today | file:line |
|---|---|---|
| **Lock screen** | `LockScreenLiveActivityView` — mascot/glyph, status title, order label, detail line | `:239-265` |
| **Island expanded** | three regions: leading = mascot + order label; trailing = ETA readout; bottom = detail-or-title | `:210-224` |
| **Island compact** | leading + trailing | `:225-228` |
| **Island minimal** | a single SF Symbol, `Brand.sky` tinted | `:229-231` |
| Art | `Image(art)` when the asset exists, **SF Symbol fallback** when it does not | `:109-112` |
| ETA | `EtaReadout` — a real ticking `Text(timerInterval:countsDown:)` or `Text(since, style: .timer)`, **with a documented fallback to the status line when there is nothing to time** | `:120-136` |

Assets shipped: `mascot_cleaning`, `mascot_on_the_way`, `mascot_live`
(`LiveActivity/Assets.xcassets/`).

**Three constraints that must shape the concept rather than be discovered during implementation:**

1. **The Live Activity is not a screen.** Apple caps the lock-screen presentation's height and the
   Dynamic Island's expanded regions have fixed geometry with hard leading/trailing/bottom slots. A
   concept drawn as a free-form card will not build. **The HTML must render the four surfaces at
   their real proportions**, not as one mockup.
2. **`minimal` is one glyph.** There is no room for a redesign there beyond *which* glyph and *what
   tint*. Say that rather than drawing something impossible.
3. **The update path is push-driven and already wired.** `Sources/LiveActivity/` carries
   `CustomerLiveActivityRegistrar.swift`, `CustomerLiveActivityOrderResolver.swift` and
   `OrderEtaWindow.swift`, with three test files. **Any concept requiring a new field in the activity
   state is a backend + push-payload change**, not a redesign — and ADR-0025 governs push. **AC5
   forces that to be stated per concept.**

## Acceptance criteria

- [ ] **AC1 — HTML at `agents/backlog/attachments/`, self-contained, opens offline.** No build step,
      no CDN. Evidence: the file paths.
- [ ] **AC2 — ALL FOUR surfaces per concept, at real proportions.** Lock screen, island expanded,
      island compact, island minimal. A concept missing a surface is incomplete. Evidence: four
      renders per concept.
- [ ] **AC3 — TWO or THREE concepts that genuinely differ.** Not one design in three colours.
      Evidence: the concepts plus a one-paragraph rationale each.
- [ ] **AC4 — every order status the activity can be alive for is covered.** Read
      `CleanStatus` (`:36`) and the resolver, and state which statuses start, update and end an
      activity. Draw each. Evidence: the state coverage per concept.
- [ ] **AC5 — the data question is ANSWERED per concept, in the owner-facing copy.** Does this
      concept need a field the activity state does not carry today? If yes, say so on the concept —
      so approving a picture does not silently approve a backend + push-payload change under
      ADR-0025. Evidence: the stated dependency, or "uses only the state fields that exist".
- [ ] **AC6 — the ETA fallback survives, or its removal is argued.** `:120-136` already handles
      *"nothing to time"*. A concept whose whole lock screen is a countdown has no design for the
      most common state. Evidence: the fallback drawn per concept.
- [ ] **AC7 — dark and light.** The lock screen renders over the user's wallpaper in both. Evidence:
      both renders.
- [ ] **AC8 — an honest implementation estimate per concept** in `S`/`M`/`L`, naming the files, and
      **saying loudly** if a concept requires an asset that does not exist (only three mascot
      imagesets ship today) or a new push field. Evidence: the estimate table.
- [ ] **AC9 — the panel's rejected directions are recorded** per `deliberation.md`. Evidence: the
      `## Challenge` / `## Defense` / `## Verdict` trail.
- [ ] **AC10 (Gate 0.5 leg 3)** — the concept states what it did not design and every assumption it
      did not verify against the activity state or against Apple's published geometry.

## Out of scope

- **Any Swift.** `git diff --stat -- src/` must be **empty**.
- **Any implementation ticket.** None until the owner picks.
- **Android.** Live Activities are an iOS surface; Android's equivalent (an ongoing notification) is
  not in this remark.
- **The push pipeline, the registrar, the resolver and the ETA window.** They work; AC5 only asks
  whether a concept would need to change them.
- **The partner app.** No Live Activity there today (PM-checked: the whole `LiveActivity` tree is
  under `CleansiaCustomer`).

## Implementation notes

**Analyst panel with the `architect` on the platform-constraint question:** author + 2–3 challengers
+ lead. **The challenge the panel must survive is the one that kills most Live Activity designs:**
*"this is beautiful and it does not fit in the lock-screen height Apple allows / the island's
trailing region."* AC2's real-proportions requirement is what makes that challenge answerable rather
than a matter of opinion.

**⚠️ DECISION POINT after this ticket, and it is the owner's.** Concepts + AC8's estimates go to the
owner. **Only then** does the PM file implementation tickets.

**Read first:** `CleansiaLiveActivity.swift` in full, the three `Sources/LiveActivity/` files, the
three `Tests/LiveActivity*Tests.swift`, and **ADR-0025**.

**Do not open `src/cleansia_ios/CleansiaCustomer/LiveActivity/Info.plist`** — it is in the owner's
uncommitted set and the Live Activity target's plist is xcodegen-generated from `project.yml`.
Nothing here needs it.

## Status log
- 2026-08-02 — **draft (created by pm from the owner's remark #7).** The four existing surfaces, the
  asset fallback at `:109-112`, the ETA fallback at `:120-136` and the three shipped mascot imagesets
  were PM-verified at `0e4ede1b` before ticketing — so the concept starts from what is there rather
  than from a blank widget. Filed **design-first with an explicit owner decision point and no
  implementation ticket behind it**, per the owner's instruction.

## Review

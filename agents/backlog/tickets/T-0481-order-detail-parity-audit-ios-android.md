---
id: T-0481
title: DISCOVERY — audit iOS against Android across both apps' order detail, and report the whole gap list
status: draft
size: M
owner: analyst
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: [T-0482]
stories: []
adrs: [0018]
layers: [analyst]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Owner remark #4, second half (2026-08-02):** *"…Replicate, and **audit both apps for anything else
missed**."* Filed as its own discovery ticket rather than hidden inside an implementation ticket,
because an audit whose output is "and also fix these" cannot be sized, and an implementation ticket
that also audits ships neither well.

### Why this ticket exists in this shape — the owner's report is partly wrong, and that matters

The owner reported: *"iOS order detail does not match Android — no progress bars, no mascot art."*
**The PM checked. On the CUSTOMER app that is false, and on the PARTNER app it is true.**

| | Android | iOS |
|---|---|---|
| **Customer** order detail — live progress hero | `OrderDetailScreen.kt:597` → `LiveProgressHero.kt` (354 lines) | **EXISTS** — `OrderDetailContent.swift:21` → `LiveProgressHero.swift` (156 lines): mascot overlay at `:88-94`, `ProgressView(value:)` at `:73`, `StepIndicator` at `:121` |
| **Partner** order detail — mascot | `OrderDetailScreen.kt:321-324` `FloatingMascot` — *"Foodora-style mascot puck… Animated WebP for InProgress, static PNG others"* | **ABSENT.** PM grepped all of `CleansiaPartner/Sources/Features/Orders/` — the only `Mascot` reference is `MascotEmptyState` on the orders **list** (`OrdersListComponents.swift:214`). `OrderDetailView.swift`'s only `ProgressView` is the loading spinner (`:54`) |

**So a developer handed "replicate Android's progress bar and mascot on iOS order detail" would go to
the customer app and find it already there.** The confirmed gap is the **partner** app — filed
separately as **T-0482**, which does not wait for this audit.

**And the customer heroes are 156 vs 354 lines.** Both have the same *elements*. Whether the
remaining 198 lines are Android-only substance or Compose verbosity is precisely what an audit
answers and a guess does not.

### One gap already found while grounding this, handed to the audit rather than fixed

The two **recurring setup** screens are not the same screen at all: Android is a **3-step wizard**
(`recurring_create_step_what_title` / `_when_ ` / `_where_pay_`, frequency cards with sublines and a
"Most popular" badge, morning/afternoon/evening time periods) at ~1071 lines; iOS is a **single-page
form** at 268 lines. **PM-measured: Android has 19 `recurring_*` string keys iOS does not; iOS has 3
Android does not (`recurring_plus_gate_*`).** That is a feature-parity gap, not a localization one
(see T-0477 / T-0478), and it is exactly the class this audit is for.

## Acceptance criteria

- [ ] **AC1 — the scope is FOUR screens, named, and the audit says so.** Customer order detail
      (Android `OrderDetailScreen.kt` + `LiveProgressHero.kt` + `OrderDetail*.kt` ↔ iOS
      `OrderDetailView.swift` + `OrderDetailContent.swift` + `LiveProgressHero.swift` + siblings) and
      partner order detail (Android `partner-app/.../orders/OrderDetailScreen.kt` ↔ iOS
      `CleansiaPartner/.../Orders/OrderDetailView.swift` + `OrderDetailContent.swift`). Evidence: the
      file inventory with line counts, per side.
- [ ] **AC2 — every gap is a ROW with a direction, a file:line on both sides, and a size.** The
      output is a table in `agents/backlog/audits/AUDIT-2026-08-XX-order-detail-parity.md`, one row
      per gap, columns: *screen · element · Android file:line · iOS file:line · direction (iOS
      missing / Android missing / both differ) · user-visible? · S/M/L*. **A gap with no file:line on
      at least one side is not a finding.** Evidence: the audit file.
- [ ] **AC3 — the audit distinguishes MISSING from DIFFERENT from DELIBERATE.** ADR-0018 governs
      design parity and several divergences in this codebase are *ruled*, not accidental (e.g.
      T-0473's ruling that the two danger components are not parity siblings). A row that flags a
      sanctioned divergence as a defect is a false finding. Evidence: each row carries either "no
      ruling found" or the ruling it was checked against.
- [ ] **AC4 — the customer LiveProgressHero 156-vs-354 delta is EXPLAINED, line by category.** State
      how many of Android's extra 198 lines are (a) elements iOS lacks, (b) Compose verbosity, (c)
      Android-only platform plumbing. This is the single question the owner's report turns on.
      Evidence: the categorized count.
- [ ] **AC5 — the recurring-setup shape gap is included and sized.** The 19-key / wizard-vs-form
      divergence above is one of the rows, with a recommendation: does iOS grow a wizard, or does the
      divergence get **ruled** as sanctioned? Evidence: the row plus the recommendation.
- [ ] **AC6 — the audit ends with a RANKED shortlist of at most 8 ticket candidates**, each with a
      one-line rationale and a proposed size. **It files no tickets** — the PM does that. An audit
      that proposes 40 tickets has not done the ranking. Evidence: the shortlist.
- [ ] **AC7 (Gate 0.5 leg 3)** — the audit states what it did **not** cover: which screens, which
      states it could not reach without a device, and every claim that is a *read* rather than a
      *run*. No suite is executed by this ticket and it must say so rather than imply coverage.

## Out of scope

- **Fixing anything.** This ticket produces a document. The one already-confirmed gap
  (partner iOS mascot/progress) is **T-0482** and runs in parallel — it is not gated on this.
- **Web.** The owner's remark named the two mobile apps.
- **Screens other than order detail and its immediate content components** — except the recurring
  row required by AC5, which is included because it was found while grounding this ticket and
  discarding it would lose it.
- **Filing tickets.** AC6 produces candidates; the PM files.

## Implementation notes

**Fan-out: FOUR analyst instances in parallel, one per side of each screen** (customer-Android,
customer-iOS, partner-Android, partner-iOS), plus **one lead** to reconcile into the single table.
That is the audit/sweep pattern from `routing.md` §"Fan-out budget" — one instance per subsystem,
because the four subsystems are independent reads. **One `reviewer` instance pairs with the lead**,
not with each reader: there is no diff to review, so the reviewer's job is Gate 0.5 leg 3 — checking
that AC7's "what we did not cover" is honest and that no row's file:line is fabricated.

**Read-only.** No source file is modified by this ticket.

**Do not open `src/cleansia_ios/**/Info.plist` or `**/project.yml`.**

**Before the iOS instances start:** `src/cleansia_ios/scripts/generate-api-clients.sh` +
`xcodegen generate` in both app dirs (**T-0474**). A reviewer reading a stale generated client
produced a false conclusion in sprint-14; an *auditor* doing it would produce a table of them.

## Status log
- 2026-08-02 — **draft (created by pm from the owner's remark #4, second half).** Filed as discovery,
  separate from T-0482, per the owner's explicit instruction not to hide the audit inside an
  implementation ticket. **The owner's premise was checked and corrected before ticketing:** the
  customer iOS order detail *does* have a progress bar, a mascot overlay and a step indicator
  (`LiveProgressHero.swift:73`, `:88-94`, `:121`); the confirmed absence is on the **partner** side.
  Needs no panel — an audit is not a story or a decision — but its **output feeds several**.

## Review

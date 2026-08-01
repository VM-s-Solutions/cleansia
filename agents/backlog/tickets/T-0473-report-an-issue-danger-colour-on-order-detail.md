---
id: T-0473
title: Order detail — "Report an issue" renders in the brand primary colour; the owner wants it red (iOS + Android)
status: draft
size: S
owner: analyst
created: 2026-08-01
updated: 2026-08-01
depends_on: []
blocks: []
stories: []
adrs: [0018, 0032]
layers: [analyst, architect, ios, android]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

**Owner defect report, 2026-08-01:** on the **order detail** screen, on **both** iOS and Android, the
"Report an issue" action uses the secondary colour. The owner wants it **red**.

**One decision applied twice**, hence one ticket rather than two.

### Ground truth — PM-verified on `f649c3bd` (2026-08-01), and one correction to the report

| Platform | Call site | Token today |
|---|---|---|
| **iOS** | `CleansiaCustomer/Sources/Features/Orders/OrderDetailView.swift:300-307` | `CleansiaOutlinedButton(L10n.OrderDetail.actionReportIssue, leadingIcon: "exclamationmark.triangle", contentColor: CleansiaColors.primary, …)` |
| **Android** | `customer-app/.../features/orders/OrderDetailScreen.kt:510-535` | `OutlinedButton(border = BorderStroke(1.dp, MaterialTheme.colorScheme.primary), colors = outlinedButtonColors(contentColor = MaterialTheme.colorScheme.primary), …)` |

**Correction, so nobody hunts for the wrong token:** the colour in force is **`primary`**, not a
`secondary` token. There is no `colorScheme.secondary` / `CleansiaColors.secondary` in play at either
call site. The owner's word "secondary" describes the button's **rank** — it is an outlined,
second-tier affordance — not its colour role. **Do not go looking for a `secondary` token to change.**

### The three red treatments that exist, and they are not interchangeable

This is the part a developer must not guess at, because all three are real, all three are "the red one",
and they produce visibly different buttons.

| # | Treatment | Where | Shape |
|---|---|---|---|
| **1** | **iOS `CleansiaDangerButton`** | `CleansiaCore/Sources/CleansiaCore/Components/CleansiaButton.swift:157` | An **error-tinted surface**: `error.opacity(0.12)` fill + `error` glyph & label + `error.opacity(0.4)` hairline. Catalog law at `agents/knowledge/patterns-mobile.md:245` — *"the ONE way"*. Consumed today by the customer profile delete-row (`ProfileTab.swift:42`) and the delete-account confirm (`DeleteAccountView.swift:131`). |
| **2** | **Android `CleansiaDestructiveButton`** | `core/.../ui/components/CleansiaButton.kt:101` | A **FILLED, fixed-red container in both themes**, deliberately **NOT** `colorScheme.error`. Its own doc (`:80-99`) carries the argument: in dark mode `error` is red-300, so a container painted with it becomes the **highest-luminance element on a Slate-900 page — brighter than the Sky400 primary** — and the destroy button ends up reading as the most inviting affordance on screen. *"Danger must not out-rank the primary; it must read as danger."* |
| **3** | **Outlined + `error` tint** | already shipped | Exactly what **Cancel** uses on both platforms today (`OrderDetailView.swift:288-296`, `OrderDetailScreen.kt:478-503`). |

**These two Core components are not parity siblings.** iOS's is a *tinted surface*; Android's is a
*filled fixed-red container*. So "adopt the danger component on both platforms" would make the two
platforms **diverge** — an ADR-0018 design-parity concern — while closing a colour complaint. That is a
strong argument for treatment **3**, but it is the **panel's** call to make and defend, not the PM's.

### Two things that are already true in the code and shape the answer

**(a) Cancel and Report issue are adjacent.** The footer stacks them with one spacer between —
`OrderDetailScreen.kt:505-508` inserts an 8dp `Spacer` when both render; iOS puts them in the same
`VStack(spacing: Spacing.s)` at `OrderDetailView.swift:288-307`. On any order where both actions are
offered, painting Report issue with Cancel's colour leaves **two adjacent buttons of the same colour,
shape and rank** — one cancels a booking, the other files a complaint.

**(b) A shipped test documents the current pairing, and it will stay green through this change.**
`CleansiaCore/Tests/CleansiaCoreTests/OutlinedButtonColorsTests.swift:61-70`,
`testOrderDetailFooterPairsMatchAndroidRoles`, is prefaced:

> *"The footer's three tinted actions, asserted as the pairs they render: Cancel destructive, Make
> recurring + Report issue primary — matching Android's `ActionsFooter` …"*

The test body loops `[CleansiaColors.error, CleansiaColors.primary]` and asserts the **component's
colour resolver**, not the **call site**. So after this change it **still passes** while its comment
becomes false. That is a green suite carrying a lie about the screen it names — the exact shape Gate 0.5
exists for. **AC4 makes repairing it non-optional.**

### The catalog tension — recorded, not absorbed

`CleansiaDangerButton`'s catalog entry is priced **`(gate pending: FT-5)`** in
`agents/architecture/decisions/catalog-governance.md:111`, because partner
`CleansiaPartner/Sources/Features/Profile/ProfileHubContent.swift:298-320` (`LogoutRow`) **hand-rolls
the component** — PM-verified: it reproduces the fill, the glyph/label colour and the `0.4` hairline
inline. That is the **non-zero baseline**, and `process/enforcement.md:104-106` forbids making a check
blocking until its baseline is zero. So one live violation is what stands between ADR-0032's **FT-5**
and a `T1-CI` gate on this law.

**PM scope ruling: `LogoutRow` is OUT of scope for this ticket.** Different app (partner), different
screen (profile hub), different affordance (logout), and folding it in would couple a two-line colour
change on the customer order detail to a **catalog-tier promotion**. It is named here only so a
reviewer does not read its absence as an oversight. **FT-5 has no `T-*` id yet** — see `## Status log`.

**And the semantics question that must not be absorbed:** "Report an issue" is a **reporting**
affordance. Red normally means **destructive or error**; nothing is destroyed and nothing has failed.
The owner asked for red explicitly, so it **is** going red — but the catalog may need to say *why this
is an exception*, or the danger role may need a second sanctioned meaning. Filed as **`Q-DESIGN-01`**
(`blocking: no` — it does **not** gate this ticket; this ticket produces its input).

## Acceptance criteria

- [ ] **AC1 — the treatment is CHOSEN against the alternatives, not picked.** Given the three treatments
      in `## Context`, When the change lands, Then the ruling names **which** was adopted and gives a
      why-not for the other two — specifically whether "Report an issue" adopts the **existing danger
      component** or **only its colour**. Evidence: the ruling in `## Review`; before/after screenshots
      of the footer on **both** platforms.
- [ ] **AC2 — dark mode is CHECKED, not assumed.** Given dark theme on each platform, When the footer
      renders, Then the change is screenshotted with **all applicable actions visible**, and the verdict
      states explicitly whether "Report an issue" now **out-ranks** the primary "Book again" CTA above
      it. `CleansiaButton.kt:80-99` records that this is a real failure mode on this exact palette
      (`error` is red-300 in dark and out-luminances Sky400) — it is a documented trap, not a
      hypothetical. Evidence: dark-mode screenshots, both platforms.
- [ ] **AC3 — Cancel and Report issue stay DISTINGUISHABLE while adjacent.** Given an order where both
      actions render, When the footer is shown, Then a user can tell them apart by something other than
      reading the label. State the differentiator (icon, filled-vs-outlined, order, size) — **or** state
      that "label only" was accepted and why. Evidence: the adjacent-state screenshot on both platforms.
- [ ] **AC4 — the stale test comment is repaired.** Given
      `OutlinedButtonColorsTests.swift:61-70`, When this change lands, Then its comment no longer says
      "Report issue primary", **and** the verdict states explicitly whether a **call-site-level**
      assertion is warranted (the current test asserts the resolver, so it cannot see this change at
      all) — if not, say why. Evidence: the diff + the reasoning. **A green suite whose comment
      contradicts the screen is a false green.**
- [ ] **AC5 — no new catalog violation, either way.** If the change **consumes** a Core component, name
      it. If it **hand-rolls** an error-tinted outlined button, the diff states in a comment that this is
      a **reporting** affordance and **not** a claim on the destructive law — so the
      `CleansiaDangerButton` baseline at `catalog-governance.md:111` does **not** grow and FT-5 does not
      move further from zero. Evidence: the reviewer's read of the enforcer, named in `## Review`.
- [ ] **AC6 — strings untouched.** This is a colour change. No `order_action_report_issue` /
      `order_detail_report_issue` / `.xcstrings` value changes on either platform. If the panel wants a
      label change, that is a **new question**, not a silent edit. Evidence: the diff shows zero i18n
      churn. *(Note for whoever looks: `values/strings.xml:283` `order_detail_report_issue` has **no**
      code reference on Android — `OrderDetailScreen.kt:533` uses `order_action_report_issue`. Possibly
      an orphan key. **Do not clean it up here** — record it, out of scope.)*
- [ ] **AC7 (Gate 0.5)** — Android: `:core` + `:customer-app` compile + unit tests re-run **un-cached**
      (`--rerun-tasks`), task outcomes recorded. iOS: `xcodebuild build test` for `CleansiaCustomer` +
      SwiftFormat `--lint` 0.60.1 / SwiftLint `--strict` 0.65.0, with an honest statement of whether the
      app-scheme tests actually **compiled and ran**. **Leg 1:** the AC1/AC2/AC3 evidence is
      *screenshots* — say so under **leg 3**; do not invent a mutation for a colour literal. Any
      assertion you *do* add (AC4's call-site test, if the ruling wants one) is mutation-proved and named.

## Out of scope

- **The partner apps.** Partner iOS has its own `reportIssue` affordance
  (`CleansiaPartner/.../Orders/NotesAndIssuesSection.swift:67` — a neutral `CleansiaOutlinedButton` with
  no `contentColor`), and it is a **different actor doing a different thing**: a cleaner logging a
  problem on a job they are working, not a customer complaining about one. The owner's report named the
  customer order-detail screen. If partner parity is wanted, it is a follow-up.
- **`ProfileHubContent.swift:298-320` (`LogoutRow`) — the known `CleansiaDangerButton` violation.**
  Named in `## Context`, deliberately excluded, reasoning given. It is ADR-0032 **FT-5**'s work.
- **Amending the catalog law itself.** That is `Q-DESIGN-01` and it does not gate this ticket.
- **Web.** `report_issue` keys exist in the customer + partner web i18n bundles, but the PM found **no
  component reference by that name** in `apps/` or `libs/`. Do not widen on that basis — if a web
  equivalent exists under another name, it is a separate finding.
- **Any other footer action.** Book again, Make recurring and Cancel keep their current roles.

## Implementation notes

**Panel FIRST, and it is step 1 of the dispatch — not a precondition to be waited on.** An `analyst`
panel on the semantics (what does red mean here, and does the exception need naming) with the
`architect` ruling the component-vs-token question and the ADR-0018 parity consequence. Author +
2–3 challengers + lead per `process/deliberation.md`. **The colour is not up for debate** — the owner
decided it. What the panel decides is *which* red, *what shape*, and *what the catalog says afterwards*.

**Challenge the panel should expect to face, so it is prepared:** *"treatment 3 (outlined + `error`)
matches Cancel, so it is obviously right"* — the counter is AC3: matching Cancel is exactly what makes
the two indistinguishable. And *"treatment 1/2 (the Core components) are 'the ONE way', so use them"* —
the counter is that the two components are **not** parity siblings (tinted surface vs filled container),
so consuming "the component" on each platform yields two different-looking buttons.

**Fan-out: two developer instances in parallel, one reviewer each.** Disjoint files:
- iOS: `CleansiaCustomer/Sources/Features/Orders/OrderDetailView.swift` (+ `OutlinedButtonColorsTests.swift` for AC4)
- Android: `customer-app/.../features/orders/OrderDetailScreen.kt`

**Shared-file lanes:** neither file has another sprint-14 writer (PM-checked against the lane list in
`INDEX.md`). `OutlinedButtonColorsTests.swift` is uncontended.

**Never read or modify `src/cleansia_ios/**/Info.plist` or `**/project.yml`.** Nothing here needs them.

**Read `agents/knowledge/patterns-mobile.md` first** — the `CleansiaDangerButton` entry at `:245` and
the `onError`-on-`error` contrast trap it records are directly relevant.

## Status log
- 2026-08-01 — **draft (created by pm from the owner's defect report).** Both call sites, the three
  competing red treatments, the adjacency problem and the stale test comment were **PM-verified against
  the code at `f649c3bd`** before ticketing, not taken from the report. **One correction to the report
  made in the ticket:** the token in force is `primary`, not `secondary`.
- 2026-08-01 — **`Q-DESIGN-01` filed** (`questions/open.md`, `blocking: no`): "Report an issue" is a
  **reporting** affordance and red means destructive/error on both design systems. The owner asked for
  red, so it ships red — what is open is whether the catalog names this as an **exception** or the danger
  role gains a **second sanctioned meaning**. **It does not gate this ticket**; this ticket produces its
  input. Filed rather than absorbed, deliberately.
- 2026-08-01 — **scope ruling: `ProfileHubContent.swift:298-320` (`LogoutRow`) is OUT.** It is the live
  `CleansiaDangerButton` violation that keeps that catalog entry at `(gate pending: FT-5)` per
  `catalog-governance.md:111`, because `enforcement.md:104-106` forbids gating a check whose baseline is
  non-zero. Excluding it keeps a two-line colour fix from carrying a catalog-tier promotion.
  **⚠️ Open item for the next PM pass, recorded so it is not lost: ADR-0032's FT-5 has no `T-*` ticket
  id.** It is named in the ADR (`0032-…md:472`) and in `catalog-governance.md:111` as the ticket that
  discharges `(gate pending:)`, but no ticket file exists. **Not filed here** — the owner asked for two
  new tickets and this is not one of them; file it deliberately, not as a side effect.
- 2026-08-01 — **stays `draft` on the panel** (DoR item 2 — AC1's ruling is the ticket's core content and
  cannot be pre-written by the PM). `depends_on: []`; nothing sequences ahead of it; dispatchable today
  with the panel as step 1. Sized **S**: two call sites, one comment repair, one ruling.

## Review
<!-- panel ruling (AC1) · dark-mode verdict (AC2) · the stated differentiator (AC3) · the test-comment
     repair + the call-site-assertion decision (AC4) · the enforcer read (AC5) go here -->

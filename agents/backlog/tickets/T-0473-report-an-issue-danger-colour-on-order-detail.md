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
- 2026-08-01 — **implemented on both platforms in one change** (`fix/T-0473-report-issue-red`, targeting
  PR #184). Ruling: **the colour, not the component** — treatment 3, `error` on the existing outlined
  shape, both platforms. Full reasoning + the AC2/AC3/AC4/AC5 verdicts in `## Review`.
  **Verified:** Android `:customer-app` compile + `testDebugUnitTest` `--rerun-tasks --no-build-cache`
  → **326 tests, 0 failures**; iOS on the **16.4 floor** — `CleansiaCore` **519 pass**, `CleansiaCustomer`
  **701 pass**, `CleansiaPartner` pass; SwiftFormat 0.60.1 `--lint` **0/662 need formatting**, SwiftLint
  0.65.0 `--strict` **0 violations in 515 files**. Both new assertions **mutation-proved** (revert the
  token → 3 iOS / 3 Android tests fail; collapse the glyph → the AC3 test fails; swap the label key →
  the AC6 test fails).
  **Outstanding:** the AC1/AC2/AC3 **screenshots** are not discharged — they need a signed-in DEV session
  with an order in Confirmed and one in Completed-with-Plus. The dark-mode rank claim is backed by
  measured luminance/contrast instead; see `## Review` AC2.
- 2026-08-01 — **stays `draft` on the panel** (DoR item 2 — AC1's ruling is the ticket's core content and
  cannot be pre-written by the PM). `depends_on: []`; nothing sequences ahead of it; dispatchable today
  with the panel as step 1. Sized **S**: two call sites, one comment repair, one ruling.

## Review

### AC1 — the ruling: **the colour, not the component. Treatment 3, on both platforms.**

"Report an issue" now renders `error` on the **existing outlined shape** — `contentColor` + border on
both platforms, exactly the pair Cancel already uses. It borrows the destructive **palette**; it does
**not** adopt either danger component.

**Why not treatment 1 (iOS `CleansiaDangerButton`) — three independent reasons, any one sufficient:**

1. **It is a semantic claim, not a style.** `CleansiaButton.swift:182` is `Button(role: .destructive)`.
   That role is surfaced by the platform — VoiceOver and every system-rendered context read it as
   "this destroys data". "Report an issue" pushes a complaint form. Adopting the component would put a
   false promise in the accessibility tree in order to settle a colour question.
2. **Its Android counterpart is a different button.** iOS's is a *tinted surface*; Android's is a
   *filled fixed-red container*. "Adopt the danger component on each platform" produces two visibly
   different buttons — an ADR-0018 divergence **created while closing a parity complaint**.
3. **It grows the very baseline FT-5 needs at zero.** Adding a non-destructive consumer to
   `CleansiaDangerButton` widens what the destructive law means, which is the opposite of what
   `catalog-governance.md:111` is waiting for.

**Why not treatment 2 (Android `CleansiaDestructiveButton`):** all of the above, plus it is **filled**.
On a Completed order the footer already carries a filled primary "Book again". A filled red button at
the bottom of that stack out-shouts it — the exact rank inversion `CleansiaButton.kt:80-99` was written
to prevent, arriving through the front door instead of through dark mode.

**What we did adopt from treatment 3, and what we did not.** The token is Cancel's; the shape is
Cancel's. The differentiator is AC3, below — and it is thinner than it should be. See the note to
`Q-DESIGN-01` at the end.

### AC2 — dark mode: Report issue does **not** out-rank Book again. Measured, not eyeballed.

Both platforms resolve `error` to the same pair (**#B91C1C** light / **#FCA5A5** dark) and `surface` to
the same pair (white / **#1E293B**), so one set of numbers covers both:

| | contrast on the footer surface |
|---|---|
| light — `error` #B91C1C on white | **6.47:1** (WCAG AA normal text) |
| dark — `error` #FCA5A5 on slate-800 | **7.71:1** |
| dark — `primary` #38BDF8 on slate-800 (the Make recurring row) | 6.83:1 |

The `CleansiaButton.kt:80-99` trap is **real and does not fire here**, and the distinction is the whole
point: red-300's relative luminance (**0.5032**) genuinely *is* higher than Sky400's (**0.4401**), so a
red-300 **container** would out-luminance the primary. This change paints a **1dp stroke plus the glyph
and label**. The button's area stays slate-800 (0.0218) while "Book again" fills its entire 48dp pill
with Sky400. The filled primary keeps roughly two orders of magnitude more coloured area. **Verdict:
Report issue reads as the lowest-rank action in the stack in both schemes.**

⚠️ **Not verified: device screenshots.** Reproducing the footer needs a signed-in session against DEV
with an order parked in Confirmed and another in Completed-with-Plus. The rank claim above is a
luminance/contrast computation, which is *stronger* evidence for the specific question AC2 asks than a
screenshot would be — but the AC asks for screenshots and this does not discharge them. **Owner or QA
still owes the four shots** (Confirmed + Completed, light + dark, per platform).

### AC3 — the differentiator: **the glyphs, plus fixed order.** Stated honestly as the weak leg.

The adjacency is narrower than it first looks, and this is load-bearing. From
`OrderStatusMapping.swift:42-58` / `OrderDetailScreen.kt:227-247`, exactly **one** status renders both:

| status | footer |
|---|---|
| New / Pending | Cancel only |
| **Confirmed** | **Cancel + Report issue** — the AC3 case, and there is **no primary CTA on screen** |
| OnTheWay / InProgress | Report issue only |
| Completed | Book again (+ Make recurring w/ Plus) + Report issue — the AC2 case, **no Cancel** |
| Cancelled | no footer |

So AC2's and AC3's worry states are **mutually exclusive**. On Confirmed the differentiator is
`xmark.circle` vs `exclamationmark.triangle` (iOS) / `Icons.Outlined.Cancel` vs
`Icons.Outlined.ReportProblem` (Android), plus a fixed order (Cancel always above), plus Cancel being
the only one that ever renders disabled.

**This is accepted, and it is thin.** Two same-coloured, same-shaped, same-sized pills separated by an
8dp spacer and a 18dp glyph is a real weakening. It was accepted rather than fixed because every fix
available inside this ticket is worse: matching-but-different reds means a new token (Architect call);
de-bordering Report changes its hit target and its rank in a way the owner did not ask for; reordering
breaks Android parity. **The right fix is the inverse of this ticket** — if reporting is red, then
*destructive* needs a rank **above** reporting, and in the outlined tier it currently has none. That is
`Q-DESIGN-01` input, not a silent iOS-only change.

Because the glyphs are now the entire differentiator, **they are pinned by test on both platforms** —
collapsing them onto one symbol was previously invisible to every check in the repo.

### AC4 — the stale comment, and yes, a call-site assertion was warranted

`OutlinedButtonColorsTests.swift:61-70` had its preamble rewritten. Its **body is unchanged and still
correct**: the footer still tints with exactly two role colours, so the `[error, primary]` loop holds.
What was false was the enumeration of *which action* got *which role*. The new comment states the
limitation outright — it asserts the resolver, cannot see the assignment, and *"survived Report issue
moving off `primary` onto `error` without a word"* — and points at where the assignment is pinned.

**A call-site assertion was warranted, and both platforms now have one:**

- **iOS** — glyph + tint hoisted into `OrderDetailFooterStyle` (`OrderDetailView.swift`), asserted by
  the new `CleansiaCustomer/Tests/OrderDetailFooterStyleTests.swift`. Same hoist-the-decision-out-of-
  the-view move `CleansiaOutlinedButtonColors` already made inside Core, applied one level up.
- **Android** — `ActionsFooter` is one `@Composable` with no seam and the module has no Compose test
  harness, so `OrderDetailFooterTintTest` reads the source and brace-extracts each `if (show…) {`
  block. Literal, but it is the repo's own established idiom for this exact situation
  (`NotificationsScreenTogglesTest.kt:17-21`), and it is precise: a token named elsewhere in the footer
  cannot satisfy it.

### AC5 — enforcer read: **no new violation, on either platform, either way**

- **iOS consumes a Core component**: `CleansiaOutlinedButton`, with `contentColor`/`borderColor` — the
  parameters Core added for precisely this. `CleansiaDangerButton`'s consumer set is **unchanged**, so
  the `catalog-governance.md:111` baseline does not grow and FT-5 is no further from zero.
- **Android hand-rolls a raw `OutlinedButton`** — as it already did for this button, for Cancel and for
  Make recurring, because `core`'s `CleansiaOutlinedButton` takes no content colour. **Pre-existing, not
  introduced here.** The diff carries the required in-source statement that this is a **reporting**
  affordance borrowing the palette and **not** a claim on the destructive law.
- `ProfileHubContent.swift:298-320` (`LogoutRow`) — **untouched**, per the PM's scope ruling.

### AC6 — strings untouched

Zero i18n churn on either platform. `order_action_report_issue` and `.xcstrings` are unchanged, and the
Android test pins the label key so a colour change cannot quietly become a relabel.

**Recorded, not fixed (out of scope, as instructed):** `values/strings.xml:283`
`order_detail_report_issue` still has no code reference — `OrderDetailScreen.kt` uses
`order_action_report_issue`. Confirmed orphan; left alone.

### Harvested back

`patterns-mobile.md` gains a short clarification under the destructive-affordance entry: **a component
colour-resolver test does not cover the call site's choice of colour**, and a screen whose styling is
plain arguments should hoist them into a value type that a test can name. That is a testability
clarification, not a redefinition of the destructive law — **the "what does red mean now" question is
deliberately left to `Q-DESIGN-01` / the Architect.**

### Evidence for `Q-DESIGN-01`

This ticket's honest finding: the design system now has **two sanctioned meanings for `error`** on the
customer order detail — *destructive* (Cancel) and *reporting* (Report issue) — distinguished by
nothing but an icon, on the one screen that shows both. Red is no longer sufficient to tell a user what
a button will do. The catalog should either name reporting as an explicit exception **or** give
destructive a rank above it in the outlined tier. It cannot keep saying red means destructive.

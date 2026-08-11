---
id: T-0536
title: The lint baseline is 25 failing projects — measure it, split it, then make lint blocking
status: draft
size: L
owner: pm
created: 2026-08-04
updated: 2026-08-04
depends_on: [T-0534]
blocks: []
stories: []
adrs: [0031]
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 15
source: `e4dd27f5` — *"The lint baseline is 25 failing projects, not the 3 I stated."* Filed by the PM
  in the sprint-15 reconciliation. **This is the "lint-cleanup ticket" that `frontend-ci.yml:71` already
  promises in a comment and that has never existed as a ticket.**
---

## Context

`.github/workflows/frontend-ci.yml:67-73` runs lint with `continue-on-error: true`, and says why:

> Lint is INFORMATIONAL for now (continue-on-error): the lint baseline has pre-existing debt in several
> libs (module-boundary, a11y, unused-var), so making it blocking today would red every PR on debt the
> change didn't introduce. It still runs so regressions are visible in the PR. Flip to blocking once the
> baseline is clean (tracked as a follow-up lint-cleanup ticket).

**There was no such ticket.** This is it.

The measured baseline is **25 failing projects**, not the 3 previously stated — mostly **circular
dependencies** and **escape-sequence warnings**. Until it is clean, three separate guards this sprint
built are advisory rather than enforcing: the generated-DTO literal ratchet (**T-0535**), the
module-boundary constraint (**T-0534**), and every future lint rule anyone adds.

**This ticket is deliberately filed as `L` and therefore may NOT go `ready`.**
`agents/process/ticket-lifecycle.md` is explicit: an `L` must be split before it runs. Splitting it
honestly needs a number this ticket does not have yet — *which* 25 projects, and *which* rule in each.
So the first act is measurement, and the split follows the measurement.

**Circular dependencies deserve their own warning.** They are not a style nit: they change module
initialization order, they can produce `undefined` at import time in a way that only shows under
SSR or a production build, and the customer app **is** SSR. A circular-dependency fix is a real change
with a real blast radius, not a lint cleanup — which is another reason this cannot run as one ticket.

## Acceptance criteria

> **AC0 — this ticket does not run.** It is `L`. It produces the measurement and the split, and is then
> superseded by its children. Any instance that starts editing lint errors under this id has skipped
> the process.

- [ ] **AC1 — the baseline is enumerated, not estimated.** Given `npx nx run-many -t lint --all`, When
      it is run from `src/Cleansia.App`, Then the output is captured and this ticket's body gains a table:
      **project → rule → count**. The 25 number is confirmed or corrected against that table.
- [ ] **AC2 — the circular dependencies are listed by cycle, not by project.** Given the
      `import/no-cycle` (or equivalent) failures, When they are read, Then each distinct cycle is written
      out as its participating files. A cycle spanning two libs is one item, not two.
- [ ] **AC3 — the split is filed.** Given AC1's table, When the split is made, Then each child ticket is
      **one rule in one subsystem**, sized `S` or `M`, with the count it must drive to zero. Escape-
      sequence and unused-var work may be batched per app; **each circular dependency is its own
      ticket** and carries a "does this change runtime behaviour under SSR?" AC.
- [ ] **AC4 — the flip is the LAST child, and it is one line.** Given every other child is `done`, When
      the final child runs, Then `continue-on-error: true` is deleted from `frontend-ci.yml:73` and the
      comment above it is deleted with it — **a comment explaining a workaround that no longer exists is
      the defect class this sprint has spent itself closing.** That child depends on all the others.
- [ ] **AC5 — no rule is disabled to reach zero.** Given any child, When it lands, Then it fixed the
      code, not the config. An `eslint-disable` is admissible only with a one-line reason naming why the
      rule is wrong *here*, and the reviewer gates it.

## Out of scope

- The module-boundary constraint itself — **T-0534**, which is a dependency: its AC5 produces the
  boundary-violation count that belongs in AC1's table, and running this measurement before it lands
  would produce a table that is wrong the next day.
- The generated-DTO literal count — **T-0535** owns that population.
- Backend, Android or iOS lint. Different toolchains, different baselines. (iOS in particular has its
  own ordered `swiftformat`-then-`swiftlint` gate; a red "SwiftLint" there is often SwiftFormat.)

## Implementation notes

Run from `src/Cleansia.App`. Use `--all`, not `affected` — affected is what CI uses and it is exactly
why the baseline was invisible.

**Do not "fix" a project by removing it from the lint target.** T-0537 exists because a lib that is
invisible to Nx is outside lint, test *and* the boundary guard at once; reproducing that deliberately
would be worse than the debt.

## Measurement — 2026-08-06 (AC1 + AC2), `npx nx run-many -t lint --all --skip-nx-cache`

**70 projects have a lint target. 18 fail. 139 errors, 163 warnings.** The title's *25* is stale and so
is its stated composition — see AC2 below.

| Class | Errors | Failing projects | Already gated? |
|---|---|---|---|
| **A — a11y** (`click-events-have-key-events` 34, `interactive-supports-focus` 34, `label-has-associated-control` 13, `elements-content` 2) | **83** | 10 | no |
| **B — `@nx/enforce-module-boundaries`** (buildable-from-non-buildable 14, static-import-of-lazy 4, deep-relative 1) | **19** | 4 | **YES — `module-boundaries.yml`, blocking** |
| **C — selector prefix** (`component-selector` 20, `directive-selector` 1) | **21** | 6 | no |
| **D — trivia** (`no-useless-escape` 5, `no-inferrable-types` 3, `no-output-on-prefix` 3, `dependency-checks` 2, `no-empty-function` 2, missing-peerDependencies 1) | **16** | 7 | no |
| | **139** | **18** | |

Warnings (163, non-failing): `no-non-null-assertion` 76, `no-explicit-any` 52, `no-unused-vars` 33,
`use-lifecycle-interface` 1, one stale `eslint-disable` directive.

### AC2 — circular dependencies: **ZERO**

`import/no-cycle` and the Nx circular-dependency message **do not appear anywhere in the run**. The
class the ticket called the bulk of the baseline was retired by T-0534's cycle work
(`partner-services ↔ partner-stores`, `customer-*`, `admin-*`). There is no cycle to enumerate, so
AC2 is satisfied vacuously and **AC3's "each circular dependency is its own ticket" has no members.**

The other half of the ticket's stated composition is wrong by the same margin: escape sequences are
**5 of 139 (3.6 %)**, all in one file (`libs/core/services/src/lib/validators/custom-validators.ts`,
lines 69 and 156). The real baseline is 60 % accessibility.

### Two facts that change the shape of the split

- **Class B is already blocking and flipping this step adds nothing for it.** The 19 errors are
  byte-identical to `check-module-boundaries.mjs`'s recorded set (14 + 4 + 1), which runs in its own
  repo-root workflow with no `continue-on-error`. Counting them toward "the baseline that blocks the
  flip" double-counts a gate that already exists.
- **17 of the 139 have ONE cause: `libs/shared/components` carries a `package.json`.** That is all 14
  buildable-from-non-buildable errors plus the 3 `package.json` errors in class D. It is not cleanup —
  it is a publishable-or-not decision about that lib, and it wants an owner ruling, not a lint ticket.

### Sizing correction for class A

83 errors is **49 elements**, not 83 edits: `click-events-have-key-events` and
`interactive-supports-focus` fire as a *pair* on the same 34 elements (34 + 34), leaving 13 labels and
2 empty buttons. The fix idiom is already written down in `patterns-frontend.md` ("Filter-drawer
backdrops must be the lint-clean a11y variant"). Distribution: 24 files across 10 projects, top five
are `pay-period-management` 12, `order-photos` 8, `photo-gallery` 6, `admin-photo-gallery` 6,
`order-management` 6. **`components` holds 13 across 6 files** and feeds all three apps.

### Class C is config drift, not code debt — and it is three decisions, not one

All 21 are a lint config disagreeing with the shipped selector, never a selector disagreeing with the
convention in `patterns-frontend.md`. Across the 50 eslint configs the prefix reads: `'cleansia'` ×28,
`'lib'` ×9, `'cleansia-partner'` ×7, `'app'` ×2, `['app','cleansia']` ×1.

| Sub-class | Errors | What is actually wrong |
|---|---|---|
| C1 — `cleansia.app` + `cleansia-partner.app` say `prefix: 'app'` | 3 | Drift from `cleansia-admin.app`, which already says `['app','cleansia']` and which the catalog documents as correct |
| C2 — `orders` + `profile` say `prefix: 'cleansia-partner'`, selectors are `cleansia-*` | 17 | 7 partner libs assert `cleansia-partner`; 5 comply, 2 do not. Rename 17 selectors **or** relax 7 configs — a real decision |
| C3 — `libs/shared/directives` says `prefix: 'lib'` | 1 | Nx scaffold default never updated; the catalog says lib configs use `cleansia` |

C3 is one error today but **9 configs still carry the scaffold `'lib'`**. The other 8 are silent only
because they declare no component yet — the first one added to `libs/shared/pipes` or
`libs/core/services` errors on arrival, and the fast "fix" is renaming the selector to `lib-*`, i.e.
into a catalog violation. Worth closing all 9 while the count is 1.

### Proposed split (AC3) — for the PM to file; no cycle children exist

| Child | Scope | Errors → 0 | Size |
|---|---|---|---|
| a11y-1 | `components` (6 files) — widest blast radius, unblocks nothing else | 13 | M |
| a11y-2 | `cleansia-partner-orders` (5 files) | 22 | M |
| a11y-3 | admin `order-management` + `pay-periods` (4 files) | 30 | M |
| a11y-4 | the remaining 7 admin/partner libs (9 files, ≤4 each) | 18 | S |
| C1+C3 | align the 2 app configs with admin; retire `'lib'` in all 9 shared/core configs | 4 | S |
| C2 | **decision first** (rename 17 selectors vs. relax 7 partner configs), then execute | 17 | M |
| D | escape 5, inferrable 3, output-on-prefix 3, empty-function 2 | 13 | S |
| components-pkg | **owner ruling**: is `libs/shared/components` publishable? | 17 (14 in B, 3 in D) | — |
| flip | AC4, depends on all of the above | — | S |

## Recommendation — 2026-08-06: **do not flip yet; split the step instead**

Flipping `continue-on-error` on Monday makes 18 projects red on debt no PR introduced, and the
predictable response is that the step gets disabled again. But "wait for 139 to reach zero" leaves the
**52 already-clean projects ungated for weeks**, which is the larger ongoing loss — a regression in any
of them is invisible today.

So: **split the lint step in two.** Keep the existing advisory `nx affected -t lint` for the 18 dirty
projects, and add a second, **blocking** `nx affected -t lint --exclude=<the 18>` step. That gates 52
of 70 projects (74 %) at **zero cleanup cost**, and the exclusion list is a ratchet in the right
direction: a newly created project is *not* on it, so it is born blocking, and the list can only
shrink as each child above lands. AC4's one-line flip then becomes "delete the exclusion list and the
second step", which is a smaller and much safer final change than flipping 139 errors at once.

Two conditions on that step, or it rots the way the current one did:

- The exclusion list needs its own **exact-match** check — an excluded project that has become clean
  must fail, exactly as `check-module-boundaries.mjs` does in both directions. Otherwise a cleaned
  project silently stays advisory forever.
- **Do not count class B toward the flip.** Those 19 are already blocking elsewhere; leaving them in
  the arithmetic makes the remaining work look 14 % larger than it is.

Residual work to a true flip after the split: **120 errors** (139 − 19 already gated), of which 83 are
one mechanical a11y idiom and 21 are config alignment.

## Status log
- 2026-08-04 — created `draft` by pm during the sprint-15 reconciliation. **Held at `draft` on purpose:**
  it is `L`, and `ticket-lifecycle.md` forbids an `L` going `ready`. Its first child is the
  measurement (AC1/AC2); the split (AC3) follows from it.
- 2026-08-06 — **measurement + split + recommendation recorded by frontend; AC0 respected, no lint
  error was edited and `frontend-ci.yml` was not touched.** The headline number moved twice: 25 → 18
  failing projects, and the stated composition ("mostly circular dependencies") → **zero** cycles.
  The circular-dependency children AC3 anticipated have no members. Recommendation is **not to flip**
  and to split the step instead; the flip child (AC4) stays last and now has a smaller final diff.
  Files the PM still owns: filing the 8 children above, and the `components` publishable-or-not
  ruling, which is an owner question rather than a lint child.

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->

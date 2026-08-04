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

## Status log
- 2026-08-04 — created `draft` by pm during the sprint-15 reconciliation. **Held at `draft` on purpose:**
  it is `L`, and `ticket-lifecycle.md` forbids an `L` going `ready`. Its first child is the
  measurement (AC1/AC2); the split (AC3) follows from it.

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->

---
id: T-0445
title: PROCESS — add a verification-integrity gate (mutation-prove the test, re-run don't trust, declare what could not be verified)
status: ready
size: S
owner: architect
created: 2026-07-30
updated: 2026-07-30
depends_on: []
blocks: [T-0439]
stories: []
adrs: []
layers: [architect, docs]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

**The owner approved this process change as part of this batch.**

`agents/process/quality-gates.md` today has no gate requiring (a) that a test be **proven to fail
without its fix**, nor (b) that a **verifying** agent **re-run** the suites itself rather than trust
the developer's report. Both gaps caused real misses recently:

1. **A security test that passed identically before and after the fix.** The suite was green, so the
   ticket advanced — but the test never discriminated the vulnerable code from the fixed code, so it
   proved nothing about the fix.
2. **A Gradle run that was `UP-TO-DATE`.** The task never executed; "BUILD SUCCESSFUL" was reported
   as verification of a change the build had not compiled.
3. **iOS tests that had never been compiled** — the app scheme cannot build in a worktree without
   `xcodegen`, so the "run" produced no test execution at all, and this went unstated.

The adjacent rules that already exist and are **not** sufficient:

- **Gate 6.5** (behavioral non-stub) requires that at least one test fail if the *implementation* is
  replaced with the empty/default value — but it is scoped to tickets "whose AC assert behavior" and
  to *spine* tickets, and it says nothing about the *fix* case (a bug fix where the pre-fix code is
  not a stub but is simply wrong). Miss #1 is exactly that case.
- The **verify-not-trust** block under Gate 8 already demands the orchestrator's own combined-tree run
  and calls a dev-reported PASS with no independent run "itself a FAIL" — but it is a blockquote under
  Gate 8, addressed to the orchestrator, and it does not require the run to be shown to have **actually
  executed** (miss #2) nor to name what did not execute (miss #3).
- **"Absent toolchain ⇒ DEFERRED-TO-CI, never PASS"** covers the *absent-toolchain* case, but miss #3
  was not an absent toolchain — Xcode was present; the scheme could not be generated. The rule as
  written does not catch "the tool was there and still nothing ran."

So the change is a **new gate**, not an edit to an existing one: it is about the *integrity of the
verification act itself*, orthogonal to which gate is being verified.

## Acceptance criteria

- [ ] **AC1** — A new gate exists in `agents/process/quality-gates.md`, placed in the numbered gate
      list, covering all three legs:
      1. **Mutation-prove the test.** For any ticket that fixes a defect or asserts behaviour, at
         least one test must be shown to go **RED against the pre-fix code** (revert the fix, or stub
         the changed body) and green with it. The verdict **names that test** and states how it was
         made red. A test that passes identically before and after fails this gate.
      2. **Re-run, don't trust.** The verifying agent executes the suites **itself** and records the
         command, the exit code, and the counts. A report that a suite "passed" without an execution
         the verifier performed is not evidence. The record must show the run **actually executed** —
         an `UP-TO-DATE`/fully-cached/0-tests-run result is **not** a pass and must be re-run
         un-cached.
      3. **Declare the unverifiable.** The verdict states plainly **what could NOT be verified and
         why**, naming the check and the reason (toolchain, scheme, environment, data). Silence is
         a gate failure; an honest "not verified because X" is a pass of this leg.
- [ ] **AC2** — The gate is **short and in the voice of the existing gates** — the same register and
      roughly the same length as Gate 6.5. It cites the three real misses in one compact "why this
      exists" line, following the precedent of Gates 6.5 / 8.5, without re-litigating them.
- [ ] **AC3** — The gate's relationship to **Gate 6.5** and to the **verify-not-trust** blockquote is
      stated in one line each, so a reader knows which applies when and the three do not read as
      duplicates. Evidence: the cross-references at file:line.
- [ ] **AC4** — `agents/process/routing.md` is updated wherever it enumerates gate flags (it already
      flags spine tickets for Gate 6.5 at `routing.md:20` and `:46-51`) so the PM writes the new gate's
      flag into tickets that need it. Evidence: the routing entry.
- [ ] **AC5** — The gate is **self-applying**: this ticket's own verdict demonstrates leg 3 by naming
      at least one thing about this change that could not be verified (a doc change has no suite —
      say so rather than claiming a green run).
- [ ] **AC6** — No other section of `quality-gates.md` is rewritten or reflowed. Only the insertion
      plus the minimal cross-reference edits AC3 requires. Evidence: the diff is additive.

## Out of scope

- Any tooling to *enforce* mutation-proving automatically (a mutation-testing harness). This gate is
  a review obligation, like every other gate in the file.
- Re-opening Gate 6.5 or the Gate 8 verify-not-trust blockquote beyond the one-line cross-references.
- The NSwag regen-drift guard's amendment to the "After an NSwag regen…" paragraph — that is T-0439,
  and it is **serialized behind this ticket**.

## Implementation notes

- **Routing:** `architect` rules on the gate's shape and its seam against 6.5 / verify-not-trust (this
  is a process decision, so it goes through an architect **defense panel** per
  `agents/process/deliberation.md` — author + 2-3 challengers + lead — before the text is written);
  `docs` writes the final text into `quality-gates.md` and `routing.md`. The panel's specific job is
  to defend the claim that this must be a **new** gate rather than three edits to existing ones, and
  to rule on the numbering/placement.
- **Shared-file lane:** `agents/process/quality-gates.md` and `agents/process/routing.md` are
  single-writer this sprint and this ticket owns them. **T-0439 must not touch `quality-gates.md`
  until this lands** (recorded in `blocks:`). Edit only your own hunks; never `git restore` either file.
- Numbering: the existing list runs 0, 1-8, 8.5. Fitting the new gate in without renumbering the
  others is a constraint (every ticket in the backlog cites gates by number) — the panel decides
  where, but **renumbering existing gates is forbidden**.

## Status log
- 2026-07-30 — draft (created by pm; owner-approved process change, this batch)
- 2026-07-30 — ready (no deps; DoR met; routed architect-panel → docs)

## Review
<!-- architect panel verdict + docs verdict here; AC5 requires an explicit "could not verify" line -->

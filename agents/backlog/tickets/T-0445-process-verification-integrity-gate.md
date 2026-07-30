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

### Architect (2026-07-30)

Gate 0.5 delivered at `quality-gates.md:52-92`, placed after Gate 0 as the second meta-gate — no
renumbering. Cross-refs to Gate 6.5 and the Gate 8 verify-not-trust blockquote live inside the new
gate. AC4 at `routing.md` (table row + sequencing rule 8). Dev-side echo at
`knowledge/testing.md` — the bug-fix twin of its red→green loop, which the catalog had only for the
feature case.

Placement chosen over Gate 6.6 and a trailing Gate 9: a rule about *how you run a check* is worthless
if read after the run, and Gate 0's own closing sentence already promised a build-side complement
that did not exist. Gate 0 governs how a **finding** is reported; Gate 0.5 governs how a **pass** is
earned.

No ADR. All 30 ADRs are system decisions and there is no process-ADR precedent; T-0374 shipped Gate
8.5 the same way. ADRs are immutable once accepted (`documentation.md:13`) while gates are amended in
place, so freezing an operating rule in one guarantees drift. If the owner wants this ADR'd, the
honest consequence is that a process-ADR class now exists and Gates 0, 6.5, 8 and 8.5 become
retroactively undocumented decisions.

**Gate 0.5 leg 3, self-applied:** doc-only change — NO suite exists and NO mechanical check was
executed by the author. The architect charter grants `Read, Write, Edit, Glob, Grep` and no Bash, so
`check-consistency`, `git diff` and markdown render were all UNVERIFIED by the author. The additive
claim (AC6) was **argued from the edit mechanics, not observed**. Leg 1 N/A — nothing to mutate.

*Orchestrator confirmation:* the additive claim was checked with a shell after the fact —
3 files, **59 insertions, 0 deletions**. The claim held.

### Reviewer (2026-07-30) — PASS WITH FINDINGS

Substance sound, placement right, no-ADR call independently verified (T-0374 exists and says what was
claimed). Six findings; none fatal, two bite tickets in flight this sprint:

- **F1** the leg-1 escape hatch is scoped "doc/config-only", which does not cover T-0443 — resource
  XML plus Kotlin, with screenshot evidence and nothing executable to mutate. Scope by **evidence
  type**, not ticket type.
- **F2** leg 1 authorises "stub the changed body" while the disambiguation paragraph uses stub-vs-revert
  as the 6.5/0.5 boundary. Self-contradictory on any ticket where only the stub form compiles.
- **F3** `routing.md:21` borrows Gate 6.5's trigger phrase **without its enumeration**, making the new
  rule strictly broader than the one it borrows from — the mechanism that drags T-0443 into F1.
- **F4** AC2 length bar missed ~3× (Gate 6.5 = 193 words; delivered = 557, now the second-longest gate).
- **F5** leg 3's causes omit *the reporting agent's own tool grant*, which is a **routing** obligation
  rather than a deferral — demonstrated by this very ticket.
- **F7** the cross-reference is one-directional; a reader arriving at Gate 6.5 never learns 0.5 exists.

**Gate 0.5 leg 2, self-applied by the reviewer:** `check-consistency.mjs --paths=agents` exited 0 but
scanned **0 files** — the tool targets backend/frontend/mobile source, not `agents/**.md`. Recorded as
a **non-run, not a pass**. The new cached-run clause caught a false green on its first outing, which
is the clearest evidence the gate earns its place.

Reviewer could not verify: whether the deliberation panel convened (no record on disk), the author's
leg-3 declaration (transient until this transcription), and two of the three motivating incidents
(they live in agent transcripts, not the repo — the account-takeover fix `854ce3c0` was confirmed).

**F1–F5 and F7 returned to the architect.** F6 — this transcription — closed by the orchestrator.
F8 (`quality-gates.md:3-5` names an outdated gate list) is pre-existing debt, follow-up ticket.

### Second pass — architect (2026-07-30)

F1, F2, F3, F5, F7 accepted and fixed. Notably:

- **F1** rescoped by *evidence type* rather than ticket type, so a screenshot-evidence ticket like
  T-0443 is out of leg 1 **by rule** instead of by a reviewer's analogy — and the anti-theater bound
  is named so nobody closes the gap with an asset-exists assertion.
- **F2** resolved by ruling rather than splitting: the stub form stays but is bounded to "where the
  pre-fix state doesn't compile", and the 6.5/0.5 boundary now turns on the **question asked** rather
  than the mutation used. T-0438 is the case that forced this — a revert-only rule is unexecutable on
  the very ticket it targets. One test may satisfy both legs; cite under each, mutate once.
- **F3** both `routing.md` entries now carry Gate 6.5's enumeration instead of the bare phrase, plus
  an explicit negative case — and, importantly, a clause preventing the narrowing of leg 1 from
  accidentally exempting a screenshot ticket from legs 2 and 3.
- **F5** accepted; the hole was demonstrated by this ticket's own author.

**One unrequested edit, driven by empirical evidence:** the reviewer's `check-consistency` run exited
0 having scanned **0 files**, which the original wording ("zero tests executed") did not cover — a
consistency checker is not a test. Now: *"Zero tests run — or zero files scanned — is a non-run,
however green the exit code."* The clause caught a false green on its first outing, against the very
tool the gate mandates.

### F4 — ADJUDICATED: accepted as CONCEDED-IN-PART (orchestrator, 2026-07-30)

The architect did not claim this bar was met, which is the right call. Measured properly rather than
by either estimate (`python3`, per-`### Gate` segmentation):

| Gate | lines | words |
|---|---|---|
| Gate 6.5 — the AC2 target | 13 | **209** |
| Gate 8.5 — the claimed comparator | 45 | 488 |
| **Gate 0.5 as delivered** | **37** | **526** |

So: **2.5× the AC2 target**, and by words the LONGEST gate in the file — not the second-longest the
architect believed, and 8% over the comparator it argued it had reached. Its own estimate (~485) was
~8% light and was honestly labelled an estimate.

**Accepted anyway.** AC2's "roughly the same length as Gate 6.5" was written against a single-leg
gate; this one carries three legs plus boundary rulings against two neighbours, and the reviewer's own
findings (F1, F2, F5) all landed *inside* it, adding back most of what the compression removed.
The two remaining cuts the architect named — the leg-1 worked example and the un-cached command list
— are both load-bearing: the example is house style (Gate 6.5 has one) and "record both numbers" was
an explicit owner instruction. Cutting to hit a word count would make the gate worse at the job it
exists to do.

The cost is real and is recorded rather than waved away: every agent reads this file at spawn, and
this change makes the longest thing in it a process gate. If it earns an amendment later, the
compression targets are named above.

**Diff at adjudication:** 3 files, 63 insertions, 1 deletion (the earlier 59/0 figure is stale — the
second pass introduced the Gate 6.5 cross-reference line). Re-measured by the orchestrator; the
architect has no shell and correctly declined to carry the old number forward.

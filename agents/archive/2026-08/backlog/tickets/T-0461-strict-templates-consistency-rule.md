---
id: T-0461
title: Consistency rule — pin strictTemplates true per app, so the regen guard's coverage cannot be silently weakened
status: draft
size: S
owner: architect
created: 2026-07-30
updated: 2026-08-01
depends_on: [T-0439]
blocks: []
stories: []
adrs: [0031, 0032]
layers: [architect, frontend, docs]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

Filed from the **ADR-0031 panel** (accepted; two code changes mandated before T-0439 merges, already
dispatched to its developer). The panel lead handed this to the PM as **finding M3**.

**The guard T-0439 builds can have its coverage weakened without anything going red.** Flipping
`strictTemplates` off in one app's `tsconfig.json` leaves the guard's own suite **5/5 green** while
the **template half** quietly stops catching what it was proven to catch. The guard's tests assert the
guard's behaviour; they do not assert the compiler setting the guard's template coverage *depends on*.
That is a mutation the guard cannot see — the same shape as T-0446's vacuous redaction test, one
sprint later.

**This is a repo-wide gate weakening, not a guard-local one.** `strictTemplates` also weakens the
**production build** — turning it off does not merely blind the guard, it stops Angular's template
type checker from rejecting bad bindings in shipped code. So the rule protects two things at once,
and that is the argument for pinning it mechanically rather than trusting review.

**PM ground-truthing (2026-07-30, `master` at `ce2416a0`):**

- All three apps currently carry it, all at the same line: `apps/cleansia.app/tsconfig.json:20`,
  `apps/cleansia-partner.app/tsconfig.json:20`, `apps/cleansia-admin.app/tsconfig.json:20` — each
  `"strictTemplates": true`. **The rule pins a state that is correct today**; it is not a fix.
- Coverage across libs is **59 of 65** — the two named exceptions are
  **`libs/data-access/admin-stores`** and **`libs/data-access/partner-stores`**, which have a
  `tsconfig.json` with no `strictTemplates` key. That is a scoping question, not an obvious bug (see
  AC1) — and note `partner-stores` is also T-0455's subject, so **do not "fix" it here**.

## Deliberation

**No new panel.** ADR-0031 is the accepted decision and this is its enforcement; the rule's *existence*
was deliberated. The one open judgement — **how wide the rule's scope is** — is posed as AC1 and
belongs to the implementer plus the reviewer, escalating to the ADR-0031 lead only if AC1 lands on
"widen beyond the three apps".

## Acceptance criteria

- [ ] **AC1 (scope, decided explicitly and written down)** — The rule covers **the three apps** at
      minimum, since that is what ADR-0031 mandates and what the production build depends on. Decide
      and record whether it also covers **libs**: 59/65 carry it, and the two that do not
      (`libs/data-access/admin-stores`, `libs/data-access/partner-stores`) are pre-existing. If the
      answer is "apps only", **say why in the rule's comment** so the next person does not read the
      6-lib gap as an oversight the rule missed. **Do not modify `partner-stores` here** — T-0455 owns
      that lib's config lane.
- [ ] **AC2** — Given an app `tsconfig.json` with `strictTemplates` set to `false`, removed, or absent,
      When `check-consistency.mjs` runs, Then it reports a **hard error** (`add()`, not `warn()` — the
      two-tier seam is at `:36`/`:44`) naming the file and the expected value.
- [ ] **AC3 (Gate 0.5 leg 1 — mutation proof; this ticket exists BECAUSE a green suite proved nothing)**
      — A test flips `strictTemplates` to `false` in a fixture and asserts the checker **goes red**.
      The reviewer **names that test**. A checker whose own test never sees a violating input is the
      exact failure this rule is about — do not ship the rule with the defect the rule describes.
- [ ] **AC4** — The rule has its entry in **`agents/knowledge/consistency.md`**, in the established
      section shape, stating the rule, the failure it prevents (*"the guard stays green while its
      template coverage is gone"*), and the ADR it enforces (**ADR-0031**). The ADR-0031 lead
      **deliberately did not touch `consistency.md`** to avoid a shared-file lane collision — **this
      ticket owns that edit.**
- [ ] **AC5** — `check-consistency.mjs` passes clean against `master` as it stands (all three apps are
      compliant today), so the rule lands **green** and does not import a new baseline of failures.
      Evidence: the checker's output, run by the implementer.

## Out of scope

- Fixing the two `data-access` libs' missing `strictTemplates` — unless AC1 rules them in scope, and
  even then **not `partner-stores`** (T-0455's lane).
- Any other Angular compiler flag (`strictInjectionParameters`, `strictInputAccessModifiers`, …).
  Pinning one flag well beats pinning five loosely. If the implementer thinks another belongs, **note
  it in the status log** for the PM to file.
- The T-0439 guard itself, its suite, and the two ADR-0031-mandated code changes — all already
  dispatched to T-0439's developer. **Do not touch them.**
- Flipping the lint gate to blocking — **T-0455** owns that question.

## Implementation notes

- **Archetype:** `agents/tools/check-consistency.mjs`'s existing rules, and **T-0454** is the sibling
  ticket doing the same job for a Compose rule — read it before starting so the two land in the same
  idiom rather than two.
- **⚠️ SHARED-FILE LANE — two collisions, both real:**
  - `agents/tools/check-consistency.mjs` → **T-0454 → T-0461** (T-0454 was previously recorded as
    "sole writer"; it no longer is). Serialize; do not run these two concurrently.
  - `agents/knowledge/consistency.md` → **T-0454 → T-0461** for the same reason (T-0454 also adds an
    entry). Order the two consistently across both files.
- **Line numbers move.** All three app `tsconfig.json` files have `strictTemplates` at `:20` today —
  do not hardcode a line number in the checker; match on the parsed JSON key, not on text position.
  (`libs/**/tsconfig.json` files sit at `:17` or `:18`, which is itself evidence that a positional
  match would rot.)

## Status log
- 2026-07-30 — draft (created by pm from the ADR-0031 panel, finding M3; enforcement of an accepted ADR, so no new panel)
- 2026-07-30 — **not `ready`**: `depends_on: [T-0439]` unsatisfied — pinning the coverage of a guard that has not merged (and that has two ADR-mandated changes outstanding) is premature. Also lane-blocked behind T-0454 on both files it writes.
- 2026-08-01 — **`depends_on: [T-0439]` is now SATISFIED** (merged `acf2f0bc`, PR #175, with M1/M2/M4/M6
  shipped and M5 withdrawn) — **but this ticket stays `draft` on purpose. Its premise changed twice
  since it was written, and both changes cut against the fix as specified.** See the block below. **Do
  not promote it to `ready` and do not dispatch it until that re-read is done.**

## ⚠️ 2026-08-01 — RE-READ THE SCOPE BEFORE ANYONE STARTS. The premise moved.

Everything in `## Context` above was true on `ce2416a0`. Two things landed after it, and each one
changes what the right fix is — not merely where it goes.

### 1. ADR-0032 is **accepted** (amended), and it prices this ticket's chosen enforcer as **T2-ADVISORY**

ADR-0032 D1 classifies enforcement by **where the check runs**, and puts `check-consistency.mjs`
squarely at **T2-ADVISORY** — *"runs on demand, reports, **never sets the exit code** for the
reviewer's gate … as it stands today on every stack — verified in **zero** `.github/` workflows."*
**PM re-verified on `master` at `1c8fdd00`: `grep -rn "check-consistency" .github/workflows/` returns
nothing.** The checker is in **no** CI job.

That is a direct hit on this ticket's stated purpose. AC2 asks for a **hard error** from the checker
"so T-0439's guard cannot be silently de-fanged" — but a checker nobody's CI runs cannot stop a
de-fang; it can only report one to whoever chooses to look. **An advisory enforcer for a rule whose
entire value is non-bypassability is the shape of defect this ticket was filed to prevent**, which is
uncomfortably close to shipping the bug the rule describes.

**And a T1-CI enforcer already exists and is already wired.** T-0439 shipped
`src/Cleansia.App/tools/typecheck-apps.test.mjs`, an 8-case suite, run by `frontend-ci.yml` as a
named step ("Regen-drift guard self-test") on both `pull_request` and `push: master`. Under ADR-0032
D1, *"a test in a CI job"* **is** T1-CI. The guard's own suite already discovers each app's compilation
unit **from that app's `project.json` build target** (M2), so asserting `strictTemplates` on the
discovered set is a few lines in a suite that already runs in CI — versus a rule in a checker that
runs nowhere.

**So the first question the implementer must answer, and record, is: does this rule belong in
`check-consistency.mjs` at all, or in `typecheck-apps.test.mjs`?** The panel is not being re-opened —
ADR-0031 mandated the *rule*, not its *housing* — but ADR-0032 changed what each housing is worth.

- [ ] **AC6 (NEW, and it comes before AC2) — choose the enforcer against ADR-0032's tier table and
      write the reasoning down.** If the rule lands in `check-consistency.mjs`, the entry must say
      **T2-ADVISORY** and must not claim to prevent anything; if it lands in `typecheck-apps.test.mjs`
      (or another CI-run suite), it says **T1-CI**. **"Both" is a legitimate answer** and may be the
      right one — the checker for the developer's local loop, the CI suite for the guarantee. What is
      **not** legitimate is shipping the checker rule while describing it as a gate.
- [ ] **AC7 (NEW) — ADR-0032 D2 now binds the `consistency.md` entry AC4 writes.** Any catalog entry
      that constrains call sites must carry a line of the form
      `**Enforced by:** <named enforcer> — <tier token>`, where the tier token is one of `T1-CI` /
      `T2-ADVISORY` / `T3-HUMAN` / `(gate pending: <ticket>)` / `(guidance — no gate)`. **This entry
      is the first new one written after ADR-0032 accepted** — if it ships without the declaration it
      immediately becomes part of the FT-4 cleanup sweep.
- [ ] **AC8 (NEW) — the scope split in AC1 is now DECIDABLE rather than a judgement call, and the two
      halves land at different tiers.** ADR-0032's T1-CI rule requires the baseline to be **zero**.
      **Apps: 3/3 compliant → baseline zero → T1-CI is legal on day one.** **Libs: 59/65 → baseline
      non-zero → `enforcement.md:104-106` forbids gating it**, so the lib half can only be
      `(gate pending: <ticket>)` with the canonicalization ticket named, or explicitly out of scope.
      Record which, with that reasoning — this replaces "decide and write it down" with an actual test.

### 2. `check-consistency.mjs` **changed on `master`** — the file, the lane and the semantics

`d6969fef` (PR #177) edited `agents/tools/check-consistency.mjs` directly on `master`, outside this
sprint's ticket flow. Consequences for this ticket:

- **The lane note at `:94-98` is stale.** It says `check-consistency.mjs` → **T-0454 → T-0461**. A
  third writer has already landed ahead of both. Re-read the file before assuming its shape; do not
  write against the `ce2416a0` version.
- **`--paths` semantics changed, and AC5 must be re-measured against them.** PM-run on `1c8fdd00`:
  an **absolute** `--paths` now resolves (it previously joined onto the repo root and printed
  `OK (0 files scanned)`, exit 0 — a false green for the whole class of invocations this backlog's own
  tickets instruct); and an explicit `--paths` matching nothing now exits **1** with `NOT RUN`.
  Measured: `--paths=<abs>/src/Cleansia.App/libs` → **32 violations, exit 1**;
  `--paths=src/cleansia_ios` → **NOT RUN, exit 1**. Full-repo default scan on `1c8fdd00`: **85
  violations, exit 1**.
- **AC5's "lands green" premise needs restating.** It cannot mean "the checker exits 0" — the checker
  does not exit 0 on this repo. It means **"introduces no NEW violation over the measured baseline"**,
  and the implementer must state the baseline it measured and the command that produced it, rather
  than inheriting 47 / 65 / 85 from another ticket. Those numbers are scope-specific and some predate
  the `--paths` fix.

### What did **not** change

The defect is still real and still worth pinning: all three apps carry `"strictTemplates": true`
today, flipping one off leaves T-0439's suite green while the template half stops catching what it was
proven to catch, and it weakens the **production build** as well as the guard. **AC1, AC3 and AC4
stand.** Only the enforcer choice, the tier declaration and the baseline arithmetic are re-opened.

## Review
<!-- reviewer verdict here; AC3 must name the mutation-proving test -->

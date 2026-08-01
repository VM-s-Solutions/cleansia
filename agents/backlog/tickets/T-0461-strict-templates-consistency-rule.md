---
id: T-0461
title: Consistency rule — pin strictTemplates true per app, so the regen guard's coverage cannot be silently weakened
status: draft
size: S
owner: architect
created: 2026-07-30
updated: 2026-07-30
depends_on: [T-0439]
blocks: []
stories: []
adrs: [0031]
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

## Review
<!-- reviewer verdict here; AC3 must name the mutation-proving test -->

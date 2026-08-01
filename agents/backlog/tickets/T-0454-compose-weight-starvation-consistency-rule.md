---
id: T-0454
title: Consistency rule for Compose weight-starvation (a weighted child beside an unbounded Text)
status: draft
size: S
owner: architect
created: 2026-07-30
updated: 2026-07-30
depends_on: []
blocks: []
stories: []
adrs: []
layers: [architect, android, docs]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

A reviewer on T-0442 proposed a new `agents/tools/check-consistency.mjs` rule after the bug class it
targets **nearly shipped in that very ticket**.

**The bug class**, as T-0442's own shipped comment states
(`customer-app/.../features/profile/ProfileTab.kt:266-268`):

```
// RowScope.weight only divides what is left AFTER unweighted children are measured, so an
// unbounded chip starves the name column to nothing once the label is long (ru/uk). Capping
// the chip keeps the name's share; the chip ellipsizes instead.
```

That is the failure mode in one sentence: a `Row`/`Column` that contains **both** a
`Modifier.weight(...)` child **and** an unweighted `Text` with no `widthIn(max=)` / `lineLimit`
constraint. In the T-0442 case the weighted child was the user's own name — so the defect would have
been invisible in `en` and total in `ru`/`uk`, the exact shape that survives review.

**PM verification, 2026-07-30:** `agents/tools/check-consistency.mjs` is 528 lines with four rule
groups — `checkBackend` (`:128`), `checkDisputeWrites` (`:230`), `checkFrontend` (`:291`),
`checkMobile` (`:386`) — plus a two-tier reporting seam: `add()` at `:36` (violation, fails) and
`warn()` at `:44` (advisory). So the tool already has the right shape for a rule that will have false
positives.

**Routing note:** the PM does not write rules. Per the charter and `process/deliberation.md`, a new
enforcement rule is an **architect decision** — it changes what the codebase is allowed to look like,
and a bad rule is worse than no rule because agents learn to ignore a noisy checker. Hence this ticket
exists to route the panel, not to specify the regex.

## Acceptance criteria

- [ ] **AC1** — Given the architect panel, When it finalizes, Then there is a recorded ruling in
      `agents/architecture/decisions/` covering: (a) is this a **violation** (`add`, fails the run) or
      an **advisory** (`warn`)? (b) what exactly triggers it, and what suppresses it — a
      `widthIn(max=)`, a `maxLines` + `overflow`, an explicit `size(...)`, an inline opt-out comment?
      (c) does it apply to `Column`/`height` as well as `Row`/`width`? Evidence: the decision doc.
- [ ] **AC2** — Given the rule as ruled, When it is run against the **current** `master`, Then its
      full output is triaged in `## Review`: every hit is either a **true positive** (filed or fixed)
      or a **false positive** (with the source line, and the rule adjusted or the case documented as
      accepted noise). An untriaged output list fails this AC.
- [ ] **AC3** — Given `ProfileTab.kt` at commit **`ce2416a0^`** (the pre-T-0442 state that carried the
      defect), When the rule runs against it, Then it **flags** the hero row. Given `ce2416a0` (the
      fixed state), Then it **does not**. This is the rule's own mutation proof under Gate 0.5 leg 1
      — a rule that cannot distinguish those two commits does not detect the bug it was written for.
      Evidence: both runs, both outputs.
- [ ] **AC4** — Given the rule ships, When a developer hits it, Then the message names the failure
      mode and the fix, not just the pattern (the existing rules' message style at `:36-46` is the
      reference). Evidence: the emitted message, quoted.
- [ ] **AC5** — Given the rule ships, When `agents/knowledge/patterns-mobile.md` is read, Then the
      weight-starvation pattern and its fix are documented there, so the rule is teaching something
      that is written down. Evidence: the diff.
- [ ] **AC6** — Gate 0.5 leg 2: the checker is run over a **non-zero** file count and that count is
      recorded. "0 files scanned, exit 0" is a non-run, not a pass.

## Out of scope

- Fixing every hit the new rule finds. AC2 requires them **triaged and filed**, not fixed — a sweep is
  its own ticket (or tickets), sized after the true-positive count is known.
- Any other proposed consistency rule. One rule, one panel, one review.
- Making `check-consistency.mjs` blocking in CI if it is not already — a separate decision with a
  separate blast radius.

## Implementation notes

**Architect panel required** (author + 2–3 challengers + lead). The challengers should attack, at
minimum: the false-positive rate on `Row`s containing icons and fixed-size spacers; whether a regex/
line-scanner over Kotlin can see a `Modifier` chain that spans several lines (all four existing rule
groups are line-based — check what they actually do before assuming); and whether the correct
enforcement point is a lint rule at all rather than a Compose `@Preview` at a narrow width plus a
long-string fixture, which is what would have caught T-0442 empirically.

**Shared-file lane:** `agents/tools/check-consistency.mjs` — sole writer, no other sprint-14 ticket
touches it. `agents/knowledge/patterns-mobile.md` was last written by T-0443 (`10d03f14`), which is
merged, so that lane is clear.

**Priority:** post-demo. This prevents the *next* occurrence of a class that has already been fixed in
this one — the same reasoning that put T-0439 behind the wave it guards.

## Status log
- 2026-07-30 — draft (created by pm; wave-1 finding with no home, proposed by T-0442's reviewer;
  routed to an architect panel per the charter — the PM does not author rules)

## Review
<!-- reviewer writes verdict here; AC2's triage table and AC3's two runs go here -->

---
id: T-0468
title: PROCESS — Gate 0.5 does not name the case where a build cache serves the mutation proof itself
status: draft
size: S
owner: architect
created: 2026-07-30
updated: 2026-07-30
depends_on: [T-0439]
blocks: []
stories: []
adrs: []
layers: [architect, docs]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

Filed from the **T-0441 review**, where the reviewer **caught its own evidence being served from the
Gradle build cache mid-mutation** and re-ran with `--no-build-cache`.

That was the right call, and the reason it matters is specific: **"it still compiles" was the
load-bearing half of the finding**, and a cache-served compile does not establish it. The reviewer
would otherwise have reported a compile result that no compiler produced during that run.

**Gate 0.5 does not currently name this case.** Leg 2 says *"a cached run is not a run"*
(`process/quality-gates.md:52-90`, shipped by **T-0445** this sprint) — but it is aimed at the
ordinary case: *do not present a cached green suite as a fresh one*. The failure the T-0441 reviewer
hit is sharper and less obvious:

> **A mutation that reproduces a *previous* mutation byte-for-byte will legitimately hit the cache.**

The build system is behaving **correctly** — the inputs really are identical to something it has
already built. There is no bug and no misuse. But the *purpose* of a mutation proof is to observe the
toolchain react to the mutated input, and a cache hit means **it never did**. The cache is not lying
about the artifact; it is silently answering a different question than the one the gate is asking.

This is easy to walk into precisely because mutation proofs are **repetitive by design** — you flip
the same flag, stub the same method, revert, and flip it again. The second flip is byte-identical to
the first.

## Deliberation required — NOT `ready`

**Architect panel**, per the **T-0445** / **T-0456** / **T-0460** precedent: `agents/process/*.md` and
`agents/knowledge/*.md` are not the PM's to edit, and Gate 0.5 is T-0445's artifact. Points for the
panel:

- **Amend leg 2, or add a leg?** Leg 2 already covers caching. This is arguably a sharper instance of
  the same law rather than a new one — but it is a *non-obvious* instance, and the general statement
  demonstrably did not prevent it. **Weigh discoverability against rule-count inflation.**
- **How wide?** Named here for **Gradle**, but the class is general: Nx's computation cache (already
  the reason `--skip-nx-cache` appears throughout this backlog), `dotnet build` incrementalism, Xcode's
  derived data, Jest's cache. **A rule that says "Gradle" will not stop the Nx version of it.**
- **Is it mechanically enforceable, or guidance?** Per `enforcement.md`, say which. A rule nobody can
  gate should be **written as guidance and labelled as such** rather than dressed as a law.
- **What is the cheap universal instruction?** Probably "run the mutation leg with the cache disabled"
  (`--no-build-cache`, `--skip-nx-cache`, …). The panel should give the **concrete flag per stack**,
  because a rule that requires the reader to work out the flag is one the reader will skip.

## Acceptance criteria

- [ ] **AC1** — `agents/process/quality-gates.md` names the case, in the Gate 0.5 voice, with the
      **concrete cache-disabling flag for each stack the team actually uses** (Gradle, Nx, dotnet,
      Xcode, Jest). Evidence: the diff.
- [ ] **AC2** — The rule cites the **real incident** that produced it (the T-0441 reviewer's
      `--no-build-cache` re-run), because every rule in this catalog earns its authority by naming the
      occurrence behind it.
- [ ] **AC3** — It is explicit that **the build system is not malfunctioning** in this scenario. A
      rule that reads as "caches are unreliable" will be dismissed by anyone who understands the
      cache; the point is that **a correct cache hit answers a different question than the gate asks.**
- [ ] **AC4** — `enforcement.md` records whether this is mechanically checkable, or explicitly not.
- [ ] **AC5** — Any other doc enumerating the Gate 0.5 legs is updated in the same change so the
      count does not drift. **Find them first** — grep `agents/` and `.claude/` for `Gate 0.5` /
      `leg 1` / `leg 2` / `leg 3` and show the results in the PR. (The same drift AC that **T-0460**
      carries, for the same reason: this sprint has already produced one doc citing "S1-S10" after
      S11 shipped.)

## Out of scope

- Changing any CI workflow's caching. This is a **rule about how evidence is produced**, not a build
  configuration change. If the panel thinks a workflow should disable caching, **file that
  separately** — it has a runtime cost that deserves its own argument.
- Re-litigating Gate 0.5's existing three legs.
- The `CLAUDE.md` summary — **owner-gated**; flag it if the panel thinks a line belongs there.

## Implementation notes

- **⚠️ SHARED-FILE LANE — `agents/process/quality-gates.md`: T-0445 ✅ → T-0439 → T-0468.**
  **T-0439 currently holds this lane** and has two ADR-0031-mandated changes outstanding. `depends_on:
  [T-0439]` is a **lane dependency, not a logical one** — this ticket's content does not need T-0439's.
- **Precedent to mirror:** **T-0445** (which authored Gate 0.5 itself) and **T-0456** — both are
  process changes routed as `architect` + `docs`.
- Related but distinct, and worth reading first: **T-0456** (worktree/stash) makes the argument that a
  rule should describe the **class**, not enumerate **commands**. That argument applies directly to
  AC1's "how wide?" question — resist the version of this rule that only says "Gradle".

## Status log
- 2026-07-30 — draft (created by pm from the T-0441 review; routed to architect because `agents/process/*.md` is not the PM's file)
- 2026-07-30 — **not `ready`**: awaiting the architect panel, and lane-blocked behind T-0439 on `quality-gates.md`.

## Review
<!-- architect + docs verdicts here -->

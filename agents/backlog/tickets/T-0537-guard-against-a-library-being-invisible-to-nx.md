---
id: T-0537
title: A library was invisible to Nx entirely — the sweep is clean, now make the state unreachable
status: ready
size: S
owner: frontend
created: 2026-08-04
updated: 2026-08-04
depends_on: []
blocks: []
stories: []
adrs: [0031]
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 15
source: `e4dd27f5` — *"Pinning it exposed that the dashboard lib had NO Nx project at all — no
  project.json, no tags, no jest config — so it sat outside test, lint AND the module-boundary guard
  simultaneously."* Filed by the PM in the sprint-15 reconciliation, which ran the sweep the commit
  recommended.
---

## Context

While fixing the partner dashboard dropping a job when a cleaner taps "on my way", the web lane found
the dashboard lib **had no Nx project file at all**. No `project.json`, no tags, no jest config. The
consequence is the interesting part: it was outside **test**, **lint** and the **module-boundary guard**
*simultaneously*, and nothing anywhere reported that. Three guards, one silent hole, zero signal.

It is now registered (`libs/cleansia-partner-features/dashboard/project.json` exists at HEAD), and the
two pre-existing lint errors that surfaced on registration were fixed so it lands green.

**The sweep the commit asked for has been run.** PM, 2026-08-04, from `src/Cleansia.App`: enumerate
every lib root (a directory containing `src/index.ts`) and report any without a sibling `project.json`.

```
find libs -name index.ts -path "*/src/index.ts" | while read f; do
  d=$(dirname $(dirname "$f")); [ -f "$d/project.json" ] || echo "MISSING: $d"; done
```

**Result: 64 lib roots, 0 missing.** There are no other libs in this state today. So the remaining work
is not a cleanup — it is making the state **unreachable**, because the failure is silent by
construction and the next lib scaffolded by hand will reproduce it.

## Acceptance criteria

- [ ] **AC1 — a guard fails when a lib root has no Nx project.** Given a directory under `libs/` with a
      `src/index.ts` and no `project.json`, When the guard runs, Then it fails and names the directory.
      **Evidence: mutation proof** — temporarily rename one `project.json`, show the failure, restore.
- [ ] **AC2 — the guard runs in CI and can set an exit code.** Given the guard, When it is wired, Then
      it runs on a workflow that actually triggers for web changes and its failure is **blocking**.
      ⚠️ Do **not** attach it to the lint step: that step is `continue-on-error: true`
      (`frontend-ci.yml:73`), so a guard placed there can never fail the build. This is the same defect
      as `check-consistency.mjs`, which ADR-0038's CH-P6 found *"appears in zero workflow files and can
      therefore never set an exit code"*. Put it where it can go red.
- [ ] **AC3 — the guard fails on an empty corpus.** Given the enumeration finds **zero** lib roots (a
      moved directory, a changed glob), When the guard runs, Then it **fails** rather than reporting
      success. ADR-0032 D3: an empty SCAN is illegal even where an empty RESULT is legal. A guard that
      goes green because it looked at nothing is the failure mode this ticket exists to prevent.
- [ ] **AC4 — the guard also asserts `tags`.** Given a registered lib, When the guard runs, Then a
      `project.json` with no `tags` array fails too. Registration without tags puts the lib back outside
      the module-boundary constraint, which is half of the original hole. **Coordinate with T-0534**,
      which defines the tag vocabulary; if T-0534 has not landed, assert *presence*, not *value*, and
      say so.
- [ ] **AC5 — the sweep result is re-run and recorded, not inherited.** Given this ticket runs later
      than 2026-08-04, When it starts, Then the implementer re-runs the enumeration above and records the
      count in the status log. The PM's zero is evidence for today, not a standing guarantee.

## Out of scope

- The dashboard registration itself and its two lint fixes — already shipped in `e4dd27f5`.
- Fixing the wider lint baseline — **T-0536**.
- Defining the tag vocabulary — **T-0534**.

## Implementation notes

**Archetype:** `agents/knowledge/consistency.md` — tree-walking guards (must be able to fail; must fail
on an empty corpus).

The precedent to copy is the offerability parity script: a dependency-free Node script **outside the Nx
workspace** (uncacheable by construction) with its own repo-root workflow. That shape was chosen because
Nx would otherwise serve a cached green — exactly the hazard here, since this guard is about projects Nx
does not know exist.

**No-decision note:** the rule is already implied by `consistency.md`; this is mechanical enforcement of
it. No panel.

## Status log
- 2026-08-04 — created `ready` by pm during the sprint-15 reconciliation. **The sweep `e4dd27f5`
  recommended is DONE and reported above: 64 lib roots, 0 missing** — the dashboard lib was the only
  one. Scope narrowed accordingly from "find the others" to "make it unreachable", which is why this is
  `S` rather than `M`.

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->

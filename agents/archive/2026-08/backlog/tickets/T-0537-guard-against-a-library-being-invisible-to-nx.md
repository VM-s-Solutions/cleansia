---
id: T-0537
title: A library was invisible to Nx entirely — the sweep is clean, now make the state unreachable
status: done
size: S
owner: frontend
created: 2026-08-04
updated: 2026-08-05
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

- [x] **AC1 — a guard fails when a lib root has no Nx project.** Given a directory under `libs/` with a
      `src/index.ts` and no `project.json`, When the guard runs, Then it fails and names the directory.
      **Evidence: mutation proof** — temporarily rename one `project.json`, show the failure, restore.
- [x] **AC2 — the guard runs in CI and can set an exit code.** Given the guard, When it is wired, Then
      it runs on a workflow that actually triggers for web changes and its failure is **blocking**.
      ⚠️ Do **not** attach it to the lint step: that step is `continue-on-error: true`
      (`frontend-ci.yml:73`), so a guard placed there can never fail the build. This is the same defect
      as `check-consistency.mjs`, which ADR-0038's CH-P6 found *"appears in zero workflow files and can
      therefore never set an exit code"*. Put it where it can go red.
- [x] **AC3 — the guard fails on an empty corpus.** Given the enumeration finds **zero** lib roots (a
      moved directory, a changed glob), When the guard runs, Then it **fails** rather than reporting
      success. ADR-0032 D3: an empty SCAN is illegal even where an empty RESULT is legal. A guard that
      goes green because it looked at nothing is the failure mode this ticket exists to prevent.
- [x] **AC4 — the guard also asserts `tags`.** Given a registered lib, When the guard runs, Then a
      `project.json` with no `tags` array fails too. Registration without tags puts the lib back outside
      the module-boundary constraint, which is half of the original hole. **Coordinate with T-0534**,
      which defines the tag vocabulary; if T-0534 has not landed, assert *presence*, not *value*, and
      say so.
- [x] **AC5 — the sweep result is re-run and recorded, not inherited.** Given this ticket runs later
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

- 2026-08-05 — **STAYS `ready` (PM reconciliation pass 4) — and this is the one row in this pass that the
  brief got wrong.** The reconciliation brief listed T-0537 as shipped, described as *"the dashboard lib
  registered in Nx (it had no project file at all)"*. **That is this ticket's OUT-OF-SCOPE half.** The
  registration did land — `libs/cleansia-partner-features/dashboard/project.json` exists at HEAD with
  `name: cleansia-partner-dashboard`, `tags: [scope:partner, type:feature]` and a jest target, plus
  `jest.config.ts` and three tsconfigs — but **AC1–AC5 are the guard, and the guard does not exist.**
  Verified by search, not by assumption: nothing under `agents/tools/`, `src/Cleansia.App/tools/`, any
  `*.spec.ts` or any workflow enumerates lib roots or asserts a sibling `project.json`
  (`grep -rln "src/index.ts" --include=*.mjs --include=*.ts --include=*.yml` returns **nothing**;
  `agents/tools/` holds only the two `check-*` pairs). So the silent state this ticket exists to make
  unreachable is **still reachable**, and closing the row would have deleted the only record of that.
- 2026-08-05 — **AC5 re-run, as AC5 itself requires.** `find libs -name index.ts -path "*/src/index.ts"` with a
  sibling-`project.json` test: **64 lib roots, 0 missing** at HEAD — unchanged from 2026-08-04. The zero is
  evidence for today; the guard is what makes it a guarantee.
- 2026-08-05 — **AC2's warning is now sharper, and it is the same warning this pass's mechanism proposal
  rests on.** `agents/tools/check-consistency.mjs` still appears in **zero** workflow files, so it can never
  set an exit code — the defect ADR-0038 CH-P6 found. `check-available-status-parity.mjs` avoided it by
  taking its own repo-root workflow (`.github/workflows/offerability-parity.yml`). **Copy that shape, not
  the other one.**

- 2026-08-05 — **guard built by frontend. AC1–AC5 discharged.** Three files:
  `agents/tools/check-nx-project-registration.mjs`, its self-test, and
  `.github/workflows/nx-project-registration.yml` (its own repo-root workflow, no `continue-on-error`,
  no `npm ci` — the checker is dependency-free and lives outside the Nx workspace so it is uncacheable
  by construction; asking Nx to enumerate projects Nx cannot see would be circular).

  **AC5 re-run at the top of this ticket, as AC5 requires:** `find libs -name index.ts -path
  "*/src/index.ts"` with the sibling-`project.json` test → **64 lib roots, 0 missing**, unchanged from
  2026-08-04 and 2026-08-05. The guard's own enumeration independently reads **64 lib roots, 64
  registered projects, 67 aliases into `libs/`, 3 rostered apps**, and `npx nx show projects` reports
  **71** (= 64 libs + 3 apps + 3 e2e + the workspace root), so the disk walk and Nx agree exactly.

  **Five rules.** `NX-1` lib root with `src/index.ts` and no `project.json` (AC1) · `NX-2` a
  registered project with no non-empty `tags` array (AC4) · `NX-3` a `tsconfig.base.json` alias
  importing a real directory that has no `project.json` — the original defect seen from the import
  side · `NX-4` an alias whose target does not exist · `NX-5` source under `libs/` with no project
  root anywhere beneath it (the same invisibility one step earlier, before a barrel exists to witness
  it). NX-1/NX-2/NX-3 have a **zero baseline and gate strictly**. NX-4 and NX-5 have a **non-zero**
  baseline, so per `enforcement.md:104-106` they are recorded as exact-match sets that fail in **both**
  directions — a new instance is red, and fixing a recorded one is red until its entry is deleted in
  the same change.

  **AC3 / ADR-0032 D3 anti-false-green:** a missing workspace, a missing `libs/`, zero lib roots, zero
  registered projects, a missing/unparseable `tsconfig.base.json`, zero aliases into `libs/`, and a
  missing rostered app are each a hard `P0` failure. The summary line always prints the counts it read;
  the tool never prints a bare "OK".

  **Mutation-proved (Gate 0.5), on the real tree, restored byte-exact (sha256 + clean `git status`):**

  | # | Mutation | Expected | Result |
  |---|---|---|---|
  | A | `mv dashboard/project.json` aside — the original defect verbatim | RED, names the dir | **exit 1**, `NX-1` + `NX-3` both name `libs/cleansia-partner-features/dashboard` |
  | A' | restore | GREEN | **exit 0**, sha256 `66cd8b6a…` identical, `git status` clean |
  | B | delete `tags` from `libs/shared/components/project.json` | RED (AC4) | **exit 1**, `NX-2` |
  | B' | restore | GREEN | **exit 0**, sha256 `f4b64c2b…` identical, `git status` clean |
  | C | real `tsconfig.base.json` + real app projects + an **empty** `libs/` | RED (AC3) | **exit 1**, two `P0`s: *ZERO lib roots*, *ZERO registered projects* |
  | D | stub the checker to `exit 0` | self-test RED | **23 of 24 scenarios FAIL**, exit 1 — the guard cannot rot into scaffolding |

  Mutation C ran against a scratchpad `--root=` built from **real** repo files rather than by renaming
  `libs/`: a sibling agent was live in `libs/cleansia-customer-features/profile/**` and moving the tree
  out from under them was not acceptable. The 24-scenario self-test covers the remaining empty-corpus
  and roster cases (including the DOT trap — an app renamed to `cleansia-partner-app` is a `P0`).

  **AC4 coordination with T-0534:** T-0534 is `in_progress`, so the guard asserts tag **presence**
  only and deliberately accepts an unrecognised tag value — there is a self-test scenario pinning that
  it does. All 64 libs already carry exactly one `scope:*` + one `type:*`, so the vocabulary check is
  promotable the day T-0534 lands.

  **Two pre-existing findings recorded by the guard, both for the PM to file — neither is T-0537's to
  fix:** three dangling `tsconfig.base.json` aliases (`@cleansia.app/order-details`,
  `@cleansia/cleansia-services`, `@cleansia/stores` — targets do not exist) and one orphaned source
  tree (`libs/cleansia`, an Angular generator scaffold with no barrel, no project and no alias, which
  `nx show projects` confirms is invisible). They are pinned exactly, so they cannot grow silently.

  **Catalog harvest (ADR-0032 D2):** `patterns-frontend.md` §"Module boundaries" now names the
  enforcer and its tier inline.

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->

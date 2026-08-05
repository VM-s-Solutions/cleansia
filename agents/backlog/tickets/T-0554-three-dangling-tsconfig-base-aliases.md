---
id: T-0554
title: Three dangling `tsconfig.base.json` aliases resolve to files that do not exist — close the Nx guard's NX-4 recorded set
status: ready
size: XS
owner: frontend
created: 2026-08-05
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: [0032]
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 15
source: found by the Nx registration guard shipped in `e78fb619` (T-0537) and recorded in its **NX-4**
  known set — *"Reported to the PM by T-0537"*
  (`agents/tools/check-nx-project-registration.mjs:99-104`). Pinned so it cannot grow; **never filed as
  a ticket until 2026-08-05**
---

## Context

`src/Cleansia.App/tsconfig.base.json` declares three path aliases whose target file does not exist.
PM-verified at HEAD, 2026-08-05 — each target checked with `test -e`, each alias grepped across
`apps/` and `libs/`:

| Line | Alias | Declared target | State | Live successor |
|---|---|---|---|---|
| `:45-47` | `@cleansia.app/order-details` | `cleansia-partner-features/order-details/src/index.ts` | **missing the `libs/` prefix entirely** — and even with it, no such lib exists. The real code is a *folder inside* another lib: `libs/cleansia-partner-features/orders/src/lib/order-details` | `@cleansia-partner/orders` (`:33-34`) |
| `:177` | `@cleansia/cleansia-services` | `libs/cleansia-services/src/index.ts` | no such directory | the per-app `@cleansia/{admin,partner,customer}-services` + shared `@cleansia/services` (`:174`, `:187`, `:178-179`, `:192`) |
| `:193` | `@cleansia/stores` | `libs/data-access/stores/src/index.ts` | no such directory — `libs/data-access/` holds only `admin-stores`, `customer-stores`, `partner-stores` | `@cleansia/{admin,customer,partner}-stores` (`:175`, `:181-182`, `:188-189`) |

**Zero importers.** `grep -rn` for each alias across `apps/` and `libs/` (`.ts`, `.html`, `.json`)
returns exactly one hit each — the `tsconfig.base.json` line that declares it. Nothing resolves through
them today.

**Why it still matters, given nothing imports them.** An alias is one of the three witnesses the Nx
registration guard uses to decide a library exists (`check-nx-project-registration.mjs:5-9`). A
declared alias that points at nothing is a standing invitation to `import` it — the editor offers the
completion, the import resolves in `tsconfig` terms, and the failure surfaces as a confusing build
error attributed to whoever wrote the import. All three read as predecessors of aliases that were later
split per app, left behind by the split.

**It cannot grow, and it is not fixed.** `e78fb619` shipped the guard plus
`.github/workflows/nx-project-registration.yml`, so a **new** dangling alias is red. These three are
recorded in `KNOWN_DANGLING_ALIASES` (`:99-104`) under `enforcement.md`'s zero-baseline rule — *"add
enforcement behind the cleanup, never in front of it"*. The recorded set is **not a suppression list**:
it is exact-match and fails in **both** directions (`:87-92`), so fixing one without deleting its entry
turns the guard red with *"stale entry, delete it"*.

## Acceptance criteria

- [ ] **AC1 — the three aliases are gone.** Given `src/Cleansia.App/tsconfig.base.json`, When the entries
      at `:45-47`, `:177` and `:193` are removed, Then no alias in that file resolves to a nonexistent
      target. Evidence: the diff, plus a re-run of the resolver over every alias in the file showing
      **zero** unresolved.
- [ ] **AC2 — deletion is the right call for each, and it is argued per alias.** Given each of the three,
      When the change is made, Then the ticket records why deletion beats repointing — in particular
      that `@cleansia.app/order-details`'s code is a **folder inside** `libs/cleansia-partner-features/orders`
      and is already reachable via `@cleansia-partner/orders`, so repointing would create a second alias
      into one lib's internals and defeat the lib boundary. **If any alias turns out to have a live
      intended target, this ticket stops and the PM re-routes** — an alias silently repointed at a
      different lib is a module-boundary change, not a cleanup.
- [ ] **AC3 — the recorded set is emptied in the same change.** Given
      `agents/tools/check-nx-project-registration.mjs:99-104`, When AC1 lands, Then all three entries in
      `KNOWN_DANGLING_ALIASES` are deleted **in the same commit**, per `:88-92`. A fix without the
      deletion is red; a deletion without the fix is red.
- [ ] **AC4 — the guard passes, and its self-test still does.** Given the change, When
      `node agents/tools/check-nx-project-registration.mjs` and
      `node agents/tools/check-nx-project-registration.test.mjs` are run, Then both exit **0**, and the
      summary reports **zero** NX-4 findings and **zero** known NX-4 entries. Paste both commands with
      exit codes (`.claude/agents/reviewer.md` §"Execution evidence").
- [ ] **AC5 — the workspace still resolves.** Given the alias removals, When `npx nx show projects` and a
      build of the app that owns the removed `@cleansia.app/*` alias are run, Then both succeed with no
      new unresolved-path error. Evidence: command + exit code.

## Out of scope

- **`libs/cleansia`, the orphaned source tree** — that is the guard's *other* recorded set (**NX-5**),
  filed as **T-0555**. Different rule, different constant, different fix.
- Adding aliases, renaming existing ones, or any module-boundary tag work (**T-0534**).
- The other three guard rules (NX-1/NX-2/NX-3), which have a zero baseline and gate strictly already.

## Implementation notes

**Files this ticket touches:**
- `src/Cleansia.App/tsconfig.base.json` — `:45-47`, `:177`, `:193`.
- `agents/tools/check-nx-project-registration.mjs` — `:99-104` (`KNOWN_DANGLING_ALIASES`).

⚠️ **Serialized lane with T-0555.** Both tickets delete from a recorded set in
`check-nx-project-registration.mjs`. The constants do not overlap (`:99-104` here, `:106-109` there),
but the file is one lane: **run them one after the other, never concurrently.**

An empty `KNOWN_DANGLING_ALIASES` is the intended end state — from that point NX-4 gates strictly, like
NX-1/2/3.

### Staleness detectability (sprint-15 §D3)

This ticket names a **product path under `src/`** (`src/Cleansia.App/tsconfig.base.json`), so the
candidate-3 path rule **will** flag it if that file is committed after this ticket's `updated:` date —
which is the whole point of naming it. `agents/tools/**` is excluded from that rule, so the guard file
alone would not have made this ticket detectable.

**No-decision note:** mechanical removal of three unreferenced aliases with named live successors; AC2
is the tripwire that converts it into a routed decision if any assumption fails.

## Status log
- 2026-08-05 — created `ready` by pm. Both facts re-verified at HEAD before filing: the three targets
  are absent, and the three aliases have zero importers outside their own declaration. Filed as its own
  ticket rather than folded into T-0537, whose guard is the *detector* and explicitly lists the sweep as
  out of scope.

## Review
<!-- reviewer verdict here; PM reconciles before advancing state -->

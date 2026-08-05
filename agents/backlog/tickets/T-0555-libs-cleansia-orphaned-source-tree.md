---
id: T-0555
title: `libs/cleansia` is invisible to Nx — and it is not a generator scaffold, it is a superseded copy of the live landing page
status: done
size: XS
owner: frontend
created: 2026-08-05
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 15
source: found by the Nx registration guard shipped in `e78fb619` (T-0537) and recorded in its **NX-5**
  known set (`agents/tools/check-nx-project-registration.mjs:106-109`). Pinned so it cannot grow;
  **never filed as a ticket until 2026-08-05**
---

## Context

`src/Cleansia.App/libs/cleansia/` holds three tracked source files and nothing else:

```
libs/cleansia/src/lib/cleansia/cleansia.ts      # CleansiaComponent, selector 'cleansia-home'
libs/cleansia/src/lib/cleansia/cleansia.html
libs/cleansia/src/lib/cleansia/cleansia.scss
```

No `index.ts`, no `project.json`, no `tsconfig.json`, no `tsconfig.base.json` alias. It is therefore
outside `nx test`, outside `nx lint` and outside the module-boundary constraint — **all three silently
at once**, which is exactly the state T-0537's guard was built to make unreachable.

### The guard's description of it is wrong, and the correction is the reason this is safe to close

`check-nx-project-registration.mjs:108` records it as *"orphaned generator scaffold — no index.ts, no
project.json, no alias"*. **It is not a generator scaffold.** PM-verified at HEAD, 2026-08-05:

- `cleansia.ts` declares `CleansiaComponent` (`selector: 'cleansia-home'`) and imports **ten**
  sub-components from `./components/{floating-bg,hero,features,process,benefits,services,gallery,testimonials,faq,cta}/…`.
- **None of those ten directories exist under `libs/cleansia/`** — the tree is three files deep and
  that is all of it. So the file **cannot compile**: ten unresolved imports.
- All ten exist under `libs/cleansia-customer-features/home/src/lib/home/components/`, in the
  **registered** `@cleansia-customer/home` lib (`tsconfig.base.json:132-134`, `project.json` present,
  `jest.config.ts` present).
- `cleansia.html` is an **older copy** of `home.component.html`: it still renders
  `<cleansia-floating-bg />` and lacks the `<cleansia-scroll-top>` the live template gained.

So `libs/cleansia` is the **predecessor** of `libs/cleansia-customer-features/home`, left behind when
the customer landing page moved into a feature lib (`d7ade53b` "Feat: Customer app (#11)"; last touched
`2b9164c0`). It has been dead — not merely unregistered, but non-compiling — since that move.

### Why it still matters

An unregistered tree of real-looking Angular source is a trap for the next reader: it is discoverable
by search, it looks like a component someone forgot to wire up, and it contains an *older* version of a
live template. Anyone who "fixes" it by registering it revives a stale landing page.

**It cannot grow.** `e78fb619` shipped the guard plus `.github/workflows/nx-project-registration.yml`,
so a **new** orphaned source root is red. This one sits in `KNOWN_ORPHAN_SOURCE_ROOTS` (`:106-109`)
under `enforcement.md`'s zero-baseline rule. That recorded set is exact-match and fails in **both**
directions (`:87-92`) — fixing it without deleting the entry turns the guard red.

## Acceptance criteria

- [ ] **AC1 — the tree is deleted.** Given `src/Cleansia.App/libs/cleansia/`, When the three tracked
      files (`src/lib/cleansia/cleansia.ts`, `.html`, `.scss`) and the empty directories above them are
      removed, Then `libs/` contains no source root without a project. Evidence: the diff + a re-run of
      the guard's lib-root enumeration.
- [ ] **AC2 — deletion is evidenced, not assumed.** Given the change, When it is reviewed, Then the
      ticket records: (a) zero importers — `grep -rn "lib/cleansia/cleansia"` and `cleansia-home` across
      `apps/` and `libs/` return nothing outside the tree itself; (b) the ten unresolved imports, so
      the file could not have been building; (c) the live successor
      `libs/cleansia-customer-features/home` is registered and routed. **If any of the three fails, this
      ticket stops** — the alternative disposition (register it as a real lib) is a different change and
      the PM re-routes it.
- [ ] **AC3 — the recorded set is emptied in the same change.** Given
      `agents/tools/check-nx-project-registration.mjs:106-109`, When AC1 lands, Then the
      `KNOWN_ORPHAN_SOURCE_ROOTS` entry for `libs/cleansia` is deleted **in the same commit** (`:88-92`).
      From then on NX-5 gates strictly.
- [ ] **AC4 — the guard and its self-test pass.** Given the change, When
      `node agents/tools/check-nx-project-registration.mjs` and
      `node agents/tools/check-nx-project-registration.test.mjs` run, Then both exit **0** with zero
      NX-5 findings and zero known NX-5 entries. Paste both commands + exit codes.
- [ ] **AC5 — the customer app is unaffected.** Given the deletion, When the customer app is built
      (`npm run build:cleansia-customer`), Then it succeeds, and the landing page still resolves through
      `@cleansia-customer/home`. Evidence: command + exit code.

## Out of scope

- **The three dangling `tsconfig.base.json` aliases** — the guard's *other* recorded set (NX-4), filed
  as **T-0554**.
- Any change to `libs/cleansia-customer-features/home` itself, including reconciling the two templates.
  The live one is authoritative; the dead one is being removed, not merged.

## Implementation notes

**Files this ticket touches:**
- `src/Cleansia.App/libs/cleansia/src/lib/cleansia/cleansia.ts`
- `src/Cleansia.App/libs/cleansia/src/lib/cleansia/cleansia.html`
- `src/Cleansia.App/libs/cleansia/src/lib/cleansia/cleansia.scss`
- `agents/tools/check-nx-project-registration.mjs` — `:106-109` (`KNOWN_ORPHAN_SOURCE_ROOTS`)

⚠️ **Serialized lane with T-0554.** Both delete from a recorded set in the same guard file. The
constants do not overlap (`:99-104` there, `:106-109` here) but the file is one lane: **run them one
after the other, never concurrently.**

⚠️ **Do not `git rm` beyond the three files.** `libs/cleansia-customer-features/*` is a different tree
whose name shares a prefix.

### Staleness detectability (sprint-15 §D3)

This ticket names **product paths under `src/`**, so the candidate-3 path rule **will** flag it if any
of those three files is committed after this ticket's `updated:` date. `agents/tools/**` is excluded
from that rule, so the guard file alone would not have made it detectable — the three `src/` paths are
what make this ticket visible to the check.

**No-decision note:** deletion of a non-compiling, unreferenced, superseded tree; AC2 is the tripwire
that routes it to a decision if any premise fails.

## Status log
- 2026-08-05 — created `ready` by pm. The guard's own description ("generator scaffold") was **corrected
  during filing**: the tree is a superseded copy of the live landing page and has ten unresolved
  imports. That correction is what makes deletion the safe disposition rather than a guess, and AC3
  requires the stale description to leave the guard with the entry.
- 2026-08-05 — implemented by frontend. The three files deleted (`git status` shows exactly three `D`
  entries under `libs/`, no sibling touched); `KNOWN_ORPHAN_SOURCE_ROOTS` emptied in the same change.
  **AC2, all three premises re-verified at HEAD before deleting:**
  - **(a) Zero importers.** `lib/cleansia/cleansia`, `CleansiaComponent`, `libs/cleansia/`,
    `cleansia.html`, `cleansia.scss` → no hit anywhere in `apps/`+`libs/` outside the tree itself;
    `cleansia-home` matches only `home.component.ts:31`, the **live** component's selector (so
    registering the orphan would have duplicated a shipped selector). No `project.json`, `tsconfig`,
    route or app config references it. The single config-level mention is
    `src/Cleansia.App/graph.json` — a **stale committed Nx graph dump** from `2e37a799` (30 nodes vs.
    71 projects today) that **nothing reads** (grep for `graph.json` across the workspace: no
    consumer). It records the tree as it was *before* `d7ade53b`, and is not a live reference.
  - **(b) It could not have been building.** The tree is exactly three files; all ten
    `./components/{floating-bg,hero,features,process,benefits,services,gallery,testimonials,faq,cta}/…`
    imports resolve to nothing under `libs/cleansia/`. `d7ade53b` deleted its `project.json`,
    `jest.config.ts`, `src/index.ts`, tsconfigs, eslint config, README and spec and left these three.
  - **(c) The successor is registered and routed.** `libs/cleansia-customer-features/home` has
    `project.json` (`cleansia-customer-home`, tags `scope:customer` + `type:feature`), the barrel
    exporting `lib.routes` + `HomeComponent`, the alias `@cleansia-customer/home`, and is routed at
    `apps/cleansia.app/src/app/app.routes.ts:13`.

  **Nothing in the dead copy is newer or better** (checked, since deleting a merge would be wrong):
  `home.component.ts` is strictly ahead — `OnPush`, `CleansiaScrollTopComponent`, `TranslatePipe`, and
  the SSR above-the-fold fix in `observeAll()` (`:not(.anim-pending)` + viewport test) that the orphan
  lacks; `2b9164c0`'s scroll-animation fix was applied to **both** copies, so the orphan is not ahead
  even there. `cleansia.html` is behind by `<cleansia-scroll-top>` and still renders the dropped
  `<cleansia-floating-bg />`. `cleansia.scss` is a two-line comment pointing at
  `libs/shared/assets/src/styles/pages/cleansia-customer/`, which exists and is untouched (including
  `_home-floating-bg.scss`) — and neither component declares `styleUrls`, so it was dead inside the
  dead tree.

  Evidence: guard `0 violation(s), 0 known` exit 0; self-test all scenarios pass exit 0;
  `npx nx show projects` 71 before / 71 after, list byte-identical (the orphan was never a project —
  the point of the ticket); `npm run build:cleansia-customer` (`nx build cleansia.app --configuration=production
  --skip-nx-cache`) exit 0. Mutation-proved: re-creating a source file under `libs/cleansia` →
  `NX-5 NEW orphan source under libs/`, exit 1; restoring the recorded entry after the deletion →
  `NX-5 STALE RECORD … delete its entry`, exit 1.

## Review
<!-- reviewer verdict here; PM reconciles before advancing state -->

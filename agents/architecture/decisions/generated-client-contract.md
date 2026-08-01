# Generated API clients — the drift contract (living decision notes)

> Companion to the **immutable** `agents/backlog/adr/0031-nswag-regen-drift-is-guarded-at-regen-time.md`
> (ADR-0031, accepted 2026-07-30). The ADR is the frozen decision with its defended alternatives; this
> file is the *evolving* design notes, trade-off space, and current shape. Update this when the design
> evolves; supersede the ADR for a real decision change.
> Cross-links: `agents/process/quality-gates.md` §"After an NSwag regen…" (the binding rule),
> `CLAUDE.md` §"NSwag Client Generation" + §"Manual Steps (owner does these)", ADR-0019 (the **iOS**
> generated client, a different pipeline governed separately).

## Scope

The **web** generated clients only — the three NSwag-generated TypeScript clients under
`src/Cleansia.App/libs/core/*/src/lib/client/`. The Android/iOS generated clients come from the separate
owner-only `mobile-spec-regen` and are **not** covered by anything on this page (ADR-0031 residue #4).

---

## Current shape (as of ADR-0031, 2026-07-30)

### The three clients and who writes them

| Client | NSwag config | Output (the ONLY generated file) | Barrel |
|---|---|---|---|
| Partner | `nswag-partner.json:39` | `libs/core/partner-services/src/lib/client/partner-client.ts` | `libs/core/partner-services/src/index.ts` |
| Admin | `nswag-admin.json:39` | `libs/core/admin-services/src/lib/client/admin-client.ts` | `libs/core/admin-services/src/index.ts:3` |
| Customer | `nswag-customer.json:39` | `libs/core/customer-services/src/lib/client/customer-client.ts` | `libs/core/customer-services/src/index.ts` |

**A generated client is never hand-edited.** Regeneration is an **owner-only** step
(`quality-gates.md` §"Owner-only steps"); agents flag it as a `manual_steps` entry and block dependent
work until the owner confirms.

> ⚠️ **Stale duplicate (ADR-0031 residue #5, open).** `libs/core/services/src/lib/client/admin-client.ts`
> is written by **no** `nswag-*.json`, exported by no barrel, imported by nothing, and typechecked by
> neither the regen guard nor the three production builds — yet `CLAUDE.md`'s repo map still advertises
> `core/services/` as "NSwag-generated API clients". An agent following the map imports a file no regen
> updates. Follow-up ticket + an owner-gated `CLAUDE.md` map correction.

### The emission rule that makes a backend field a compile break

All three configs set **`markOptionalProperties: false`** (`nswag-*.json:31`). Consequence:

```
backend DTO member is NULLABLE   ──NSwag──▶   accessInstructions: string | undefined;
                                              ^^^^^^^^^^^^^^^^^^  REQUIRED key, OPTIONAL value
```

TypeScript requires the **key** to be present in every object literal. So adding one nullable field to a
backend command breaks **every existing** `new XCommand({ … })` call site at once — **122** of them
outside the generated clients today, and growing. This is the entire defect class.

### Where the check lives, and what each placement is worth

```
owner edits a backend DTO
        │
        ▼
npm run generate-{partner|admin|customer}-client       ← the ONLY regen entry points (ADR-0031 M1)
        │  nswag run  →  formatter  →  npm run typecheck
        │                               ngc --noEmit over EVERY apps/*/ compilation unit
        ▼                               (Angular compiler: TS + template diagnostics)
   ✅ PREVENTS a red master on this path — names file:line before a commit exists
        │
        │   …any other path (hand-run generator, hand-edited client, some future script)
        ▼
git push master  ──▶  frontend-ci.yml (push: master, paths-scoped)
        │              3 production builds — the AUTHORITY
        ▼
   ⚠️ ATTRIBUTES ONLY — master is already red; the red now lands on the offending
      commit within minutes instead of ambushing the next contributor's PR
        │
        ▼
   ❓ nothing prevents that red — the option that would (branch protection) is Q-CI-01
```

**Do not call these "primary and backstop."** ADR-0031 CH-3 was sustained precisely because that phrasing
implies the class is covered. One leg prevents on one path; the other prevents nothing anywhere.

### The guard's coverage, stated structurally

`tools/typecheck-apps.mjs` is handed each app's `tsconfig.app.json` — the same file the production build
target passes to `@angular/build:application` (`apps/cleansia.app/project.json:26`). Its file set is
therefore the build's compilation unit **by construction**:
`include: ["src/**/*.ts", "server.ts"]` plus everything those files transitively import, with `strict` +
`strictTemplates` inherited from the app's own `tsconfig.json:15-21`.

- ✅ **Covers:** TypeScript diagnostics and **Angular template** diagnostics over each app's unit —
  including generated types/enums reached only from a template, which plain `tsc --noEmit` cannot see
  (proven: `ngc` exited 1 with `TS2339` where `tsc` exited 0).
- ❌ **Does not cover:** bundling, budgets, SSR prerender, styles. The three production builds remain the
  authority; the prose rule "build all three before pushing" is **unchanged and still binding**.
- ❌ **Also outside:** `.spec.ts` files (excluded at `tsconfig.app.json:8-13`) and lib files unreachable
  from every app entry — but *outside by the same configuration that puts them outside a production
  build*, so the prose rule never covered them either.
- **State coverage structurally, never empirically.** "N call sites fall outside" decays with every
  commit; *the guard's tsconfig is the build's tsconfig* does not. Anything outside the guard is outside
  the three builds too. (The T-0439 `tsc --listFiles` enumeration that found exactly two such files is
  evidence *for* the identity, not a substitute for it.)

---

## Trade-off space (what was considered and why it landed where it did)

| Option | Axis | Verdict | Why |
|---|---|---|---|
| **A — typecheck chained to the regen command** | placement of a check | **CHOSEN** | Fires where the defect is *created*, before a commit exists. Mutation-provable (Gate 6.5) — three chained builds are not. **Not faster** than the builds (measured: 3 builds ≈ 120 s cold / 58.7 s warm; typecheck 28.5–69.4 s) — speed is *not* the argument. |
| B — chain the three production builds | placement of a check | rejected as mechanism, **retained as the rule** | Byte-identical to CI, but short-circuits at the first failing app (T-0438 broke all three), swings with cache state, writes `dist/`, untestable. |
| C — pre-push git hook | placement of a check | rejected | Not checked out by default, `--no-verify`-able, repo-wide tax for a one-command failure mode. |
| D — `markOptionalProperties: true` | remove the sharp edge | **rejected, BOUNDED** | Trades a loud compile error for a silent runtime bug — proven by the `accessInstructions` data-loss find. See "The open D question" below. |
| **E — branch protection / merge-via-PR** | *who may redden `master`* | **ESCALATED — Q-CI-01** | The only option on this axis; would have caught **both** incidents with zero new machinery. Constrains the **owner's** workflow → owner decides, not the panel. Composes with A; replaces nothing. |
| F — a `check-consistency.mjs` line rule | placement of a check | rejected | Required-key satisfaction is not line-local (spreads, conditional keys, variables, inheritance, generics). A type defect gets a type checker. |
| G — Nx-cache / `--incremental` the typecheck | cost | rejected | A mis-specified `inputs:` caches a green across a client change — the precise false green the guard exists to prevent. |
| H — a dedicated client-drift CI job | placement of a check | rejected (standing position **upheld**) | No job was added; the existing build gate was pointed at the branch where the damage lands. |

### The open D question (the one live thread)

D is rejected **today**, on a decisive counter-example: the `accessInstructions` compile error was the
only reason anyone discovered the booking wizard had been collecting entry instructions, rendering them
back on the summary step, and **discarding them at submit** since the field shipped
(`order-wizard.facade.ts:551`). Under D that literal compiles and the data loss continues.

But the rejection is **bounded**, for two recorded reasons:

1. **Measured cost of the current posture.** T-0438: three broken call sites, **one** semantic catch, two
   noise (wired `undefined`/`false`). Signal:noise **1:2**. *Today that is a good trade* — the signal was a
   shipped data-loss bug; the noise was two one-line compile-time edits.
2. **The ratio scales badly.** Signal is ~1 per added field (the place that should wire it); noise scales
   with call-site count (122 and rising). So the trade degrades monotonically with codebase growth.

- **Revisit trigger:** one regen breaking **more than 10** call sites for a *single* added optional field
  → D gets its own ADR, not a footnote.
- **Unresolved premise:** whether ASP.NET marks non-nullable value types `required` in the emitted schema
  — i.e. whether `removePhoto: boolean` would even become optional under D. **Never observed.**
- **The free experiment:** at the next owner regen (T-0446 is imminent), one extra run with
  `markOptionalProperties: true` into a scratch output, diffed and discarded, settles it. **Record the
  result here**, whether or not D is ever adopted.

| D question | State |
|---|---|
| Does `markOptionalProperties: true` make nullable *reference* members optional? | expected yes — unverified |
| Does it change `removePhoto: boolean` (non-nullable value type)? | **UNKNOWN — the blocking unknown** |
| Diff size across all three clients | unknown (owner-only regen) |
| Result of the scratch experiment | _(fill in after the next regen)_ |

---

## Invariants (what a reviewer enforces)

1. **A generated client is never hand-edited.** The regen is owner-only; agents flag `manual_steps`.
2. **No `package.json` script invokes NSwag without ending in `npm run typecheck`** (ADR-0031 M1). A
   publicly-named script that regenerates without the guard reopens the hole silently.
3. **The guard's unit set is derived from the build target, never hardcoded and never merely non-empty**
   (ADR-0031 M2). A declared build target whose `tsConfig` is missing is a hard failure — "2 of 3 apps
   checked, green" is the T-0438 topology.
4. **The guard is a typecheck, not a build.** The three production builds stay CI's authority and the
   prose pre-push rule stays binding verbatim. Never narrow the rule to match the guard.
5. **`ngc`, not `tsc`** — pinned by a template-diagnostic fixture so the cheap compiler cannot be swapped
   back in silently.
6. **The guard can go red.** Stubbing its body to `process.exit(0)` must fail its suite, and that suite
   runs in CI (`frontend-ci.yml:69-71`).
7. **The guard's own failures must be actionable.** It runs *inside* the owner's regen, so an
   infrastructure fault of its own (a missing/moved Angular compiler) must not read as "your regen
   failed". Resolve the `ngc` bin from `@angular/compiler-cli/package.json` rather than hardcoding
   `bundles/src/bin/ngc.js` — the path has moved across Angular majors (ADR-0031 M4).
8. **Every command a human is told about is guarded, and every guarded command is one a human is told
   about.** Two sides of one surface: no discoverable script regenerates without the typecheck (M1), and
   the preferred all-three command is documented rather than folklore (M6). Cost is *not* the argument for
   either — ADR-0031 struck speed as a justification.

9. **The guard's test fixtures live outside the repository.** `tools/typecheck-apps.test.mjs` builds its
   throwaway workspace under `os.tmpdir()` and reaches the workspace's `node_modules` via `baseUrl` +
   `paths`. This is **structural**, not configurational: since invariant 3 (M2) made every fixture carry an
   `apps/<app>/project.json`, an in-repo fixture that survived a cancelled run would be inferable by **Nx
   as a real project**. Do not "fix" a future leak with a `.gitignore` line — move the fixtures back out.
   (ADR-0031 M5 was withdrawn for exactly this reason; the ignore entry it mandated was struck as dead
   configuration that read as protective.)

10. **A green `generate-*-client` proves the tree COMPILES — not that the client regenerated correctly.**
    State the guarantee at its true width. The chain is
    `npx nswag run <config> && bash <x>-client-formatter.sh && npm run typecheck`, and it can report
    success over a bad client in **two independent ways**:
    - **Verified (2026-07-30):** none of `admin-`/`customer-`/`partner-client-formatter.sh` sets `set -e`,
      and each ends in `echo "Renaming completed successfully."` (`:18`) — so **every formatter exits 0
      even if its `sed` failed**. The `&&` chain cannot see a formatter failure on any of the three
      clients, and the success message is printed unconditionally.
    - **Unverified:** what `npx nswag run` returns on a partial or failed generation. Agents cannot run it
      (owner-only), so this is an open unknown, not a claim.

    What the typecheck *does* prove is exact and still worth having: whatever client file is on disk now
    compiles against every app compilation unit. What it cannot prove is that the file on disk is the
    freshly generated one, or that the renames applied. The three production builds share the blind spot —
    they also only see what is on disk. **Out of scope for T-0439; do not let the chain be described as
    stronger than this.** Cheap future fix: `set -euo pipefail` in the three formatters. Free observation:
    the next owner regen (T-0446) is already carrying the Option-D experiment — record `nswag run`'s
    exit-code behaviour on the same run.

**Two things that are NOT holes, written down so they are not re-derived as such:**
- A leaked fixture was never a *coverage* hole even when fixtures lived in-repo: the guard reads
  `src/Cleansia.App/apps`, and a fixture root sits one directory deeper. The hazard was Nx inference and
  committable junk, not guard blindness.
- The guard covering less than the three production builds is deliberate and exact for this defect class
  (invariant 4) — not an oversight to be closed by widening it.

## Known gaps (accepted, named, not closed)

| # | Gap | Bound / named fix |
|---|---|---|
| 1 | Nothing **prevents** a red `master` on non-regen-script paths | attribution speed only, until **Q-CI-01** is answered |
| 2 | Guard covers TS + templates, not bundling/budgets/SSR/styles | exact for this defect class; the three builds remain the authority |
| 3 | `strictTemplates` can be flipped off silently in an app's `tsconfig.json` | weakens the guard **and** the production build together → a `check-consistency.mjs` rule (ADR-0031 M3) |
| 4 | Android/iOS generated clients are a parallel, separately-governed drift surface | `mobile-spec-regen` + ADR-0019; deliberately out of scope |
| 4b | **The regen chain can report success over a bad client** — formatters always exit 0 (no `set -e`, trailing `echo`); `nswag run`'s failure exit code is unverified | invariant 10; `set -euo pipefail` is the cheap fix, and the T-0446 regen can observe the `nswag` half for free |
| 5 | **Code no gate can see** — (a) the stale duplicate `libs/core/services/src/lib/client/admin-client.ts` + a `CLAUDE.md` map that points at it; (b) app-unreachable lib files (e.g. `libs/cleansia-admin-features/template-management/.../email-template-form.facade.ts`) | follow-up ticket (a dead-export sweep, not a wider guard — widening the guard to compile unreachable files would break the structural identity above); `CLAUDE.md` edits are owner-gated |

## Rollout state

| Step | Where | State (2026-07-30) |
|---|---|---|
| ADR (placement decision + rejected options) | ADR-0031 | **accepted** (panel verdict, M1–M6 mandated) |
| `tools/typecheck-apps.mjs` + its suite | `src/Cleansia.App/tools/` | shipped in T-0439 (under review) |
| `generate-*-client` chained to the typecheck | `package.json:23-26` | shipped in T-0439 |
| **M1** — no unguarded `nswag:*` entry point | `package.json:20-22` | **required before T-0439 merges** |
| **M2** — discovery from the build target's `tsConfig` | `tools/typecheck-apps.mjs` | **required before T-0439 merges** |
| `frontend-ci.yml` `push: master` + guard self-test | `.github/workflows/` | shipped in T-0439; **no CI run has exercised the push trigger yet** |
| `quality-gates.md` pointer paragraph | `:297-306` | shipped (additive: 11 insertions, 0 deletions) |
| **M4** — guard resolves `bin.ngc` from the package manifest (or splits its "not installed" vs "path moved" message) | `tools/typecheck-apps.mjs:30-43` | **required before T-0439 merges** |
| ~~**M5** — `.typecheck-fixture-*` ignored~~ | ~~`src/Cleansia.App/.gitignore`~~ | **WITHDRAWN 2026-07-30** — fixtures moved to `os.tmpdir()`, which removes the condition; the ignore line is struck as dead config (ADR-0031 dated closure §B) |
| **M6** — `generate-clients` documented + the guard sentence | `CLAUDE.md:93-96` | **owner MANUAL_STEP** — proposed text lives in the T-0439 `## Review` |
| **M3** — `strictTemplates` consistency rule | `agents/tools/check-consistency.mjs` | follow-up ticket (PM) |
| CH-14 — stale client + `CLAUDE.md` map | `libs/core/services/` | follow-up ticket (PM); owner edits `CLAUDE.md` |
| **Q-CI-01** — branch protection for `master` | `questions/open.md` | **open — owner** |
| D experiment (`markOptionalProperties: true` scratch run) | next owner regen (T-0446) | recommended, not gating |

## Documenting this topic: cite stable anchors, not line numbers

Scoped rule for this page and for anything citing into `.github/workflows/**`, `package.json` scripts or
`project.json` targets: **cite the named thing — a step name, a job id, a YAML key, a script name — not a
line range.** ADR-0031 was bitten twice inside one ticket: a comment block added mid-file moved four of its
citations, and the *careful* remapping offered as the fix contained three off-by-ones in five entries. A
citation format whose correct maintenance is that error-prone is the wrong instrument for config files
that accrete comments.

The one case where a line range is still right: when you are pinning **what a reader ruled on at a
date** — an ADR's `## Challenge`/`## Defense`/`## Verdict` citations describe the artifact as reviewed and
must keep pointing there even after the code moves. Re-anchor *navigational* citations; leave *historical*
ones.

*(This is stated here in its topic-scoped form. Its general form — "ADRs citing CI/config files cite stable
anchors" — is a `agents/knowledge/conventions.md` candidate and is routed to the PM as a catalog-edit
proposal rather than smuggled into a topic doc where nobody would find it.)*

## Open questions / future evolution

- **Q-CI-01 (owner)** — require PRs for `master`? If yes, the `master` push build becomes largely
  redundant (kept as a paths-scoped net) and the whole "attribution vs prevention" asymmetry collapses in
  our favour. If no, gap #1 is permanent and the guard is the only prevention we have.
- **A fourth Angular app** needs no change here — coverage is derived. Under M2 that is provable rather
  than conventional; verify by checking the guard reports the new app the first time it runs.
- **If D is ever adopted**, this page changes shape rather than disappearing: the defect class moves from
  compile-time noise to a silent-under-wiring risk, and the compensating control (a wiring checklist? a
  runtime assertion? a diff review of new optional fields?) becomes the thing that needs a decision.
- **If a new generated client is added** (a fourth audience, a public API SDK), it inherits invariants
  1–6 or it is a deviation needing its own ADR.

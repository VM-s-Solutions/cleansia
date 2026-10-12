---
id: T-0818
title: Correct the docs that drifted from the code: backend pages, request logging, local test commands, owner rulings
size: XS
owner: pm
created: 2026-10-11
updated: 2026-10-12
depends_on: [T-0808]
blocks: []
stories: []
adrs: []
layers: [docs]
security_touching: false
---

## Context

Docs findings still true on master, per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets).

## Doing

- [BE-06/WCD-20] `infrastructure.md`: DEV ships the five middleware categories' redacted request and response lines at Information, as `main.bicep` says → `/architecture/request-logging`.
- [BE-08] The digest's freshness docs say that only the hold-expiry and released-window sources are bounded at sweep start. This is the zero-behaviour fix: the Wave D handover ruled "no new sweep-start upper bound".
- [BE-09] The hold resolver's role page and `security-rules.md` cite the current lines.
- [BE-10/WCD-30] `patterns-backend.md` states ADR-0039 D3.2 as shipped, and adds the one-entity-many-windows shape: one commitment read, a pure Domain twin, and a PostgreSQL agreement test.
- [WCD-19] The README and local-orchestration pages show CI's exact `dotnet test` form.
- Owner rulings [D4]: the overview's F11 paragraph records that asking after sign-in was declined on 2026-10-11, and the F10 note says the bar-less hero is final.

## NOT doing

- No ADR edit.
- No sweep of the 68 old citation misses (a separate decision).

## Acceptance criteria (done looks like)

- [x] **AC1** — Every edited sentence matches the code, the infrastructure template or Mike's ruling.
- [x] **AC2** — The repository checkers and the docs build pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-11 — Implemented: BE-06/WCD-20, WCD-19 and D4 by the docs lane; BE-08, BE-09 and BE-10/WCD-30 by the backend lane.
  - BE-06/WCD-20:
    - DEV ships the five hosts' redacted request and response lines at Information, through `main.bicep`'s `Cleansia` category.
    - On prod, only a 4xx or 5xx line ships.
    - The Sentry item is scoped to prod. It names DEV's protection: redaction and sensitive-path suppression.
  - BE-08, with no behaviour change:
    - the digest's summary, `ApplyFreshness` and `offerability-and-take.md` now say only the hold-expiry and released-window sources stop at the sweep's start, and the role page's invariant 11 bounds only the hold-expiry term;
    - so a status change during a sweep can count twice.
  - BE-09:
    - the role page cites `NewJobsDigestService.cs:202-209`, and security-rules cites `UserRepository.cs:157-204` and `:206-213`;
    - check-catalog-claims' C3B miss on `security-rules.md:114` clears, taking advisories from 155 to 154.
  - BE-10/WCD-30: `patterns-backend.md` states ADR-0039 D3.2 as shipped and adds the one-entity-many-windows shape.
  - WCD-19:
    - the README, `local-orchestration.md` and the `ci-cd.md` steps block show CI's form: one Release build, then `--no-build -- xUnit.parallelizeTestCollections=false`;
    - the README block, run under `zsh -f` in a copy of HEAD, builds and passes the unit suite 8,639/8,639.
  - D4: the F11 and F10 sentences record the 2026-10-11 rulings.
  - RED/GREEN:
    - the docs lane's assertion script fails these claims on HEAD and passes them on the edited tree;
    - the docs build passes, and so do check-docs-refs, check-catalog-claims and check-consistency.
- 2026-10-12 — Committed as `da2aa77`, with the `ci-cd.md` steps-block hunk. It builds on its own; its one code file, `NewJobsDigestService.cs`, changes comments only.

## Review

- **Catalog routing, BE-10 (`patterns-backend.md:1414-1441`).** Test 2 fires, because the hunk replaces a governing sentence. So the Architect ratified it in the ADR-0033 lead pass of 2026-10-11, and made these adjustments himself (`:1418-1428`, `:1439-1441`):
  - "lands on every shape" is narrowed, because time terms also live in `Order.OccupiesWindow`;
  - the shared filter is named as the "same call" of the read-path/write-path bullet, which the D3.2 shape would otherwise contradict;
  - a Gate 4 `T3-HUMAN` enforcer line is added, with its not-mechanizable reason;
  - the reverse-shape bullet gets its justification.
- **The security re-review objected to that "same call" sentence**, reading it as a relaxation of the "SAME CALL, not the same rule" bullet.
  - It is left as the Architect ratified it. What shipped is ADR-0039 D3.2 as amended ("sharing the *predicate* rather than the *terminal operation*", `adr-0039.md:450-452`), and a shared filter is one code path, not two kept in step by documentation.
  - Listed for Mike, who can overrule it.
- **Two deliberate additions, both named in the commit:**
  - the dispatch doc's snapshot sentence, from BE-10's fix sketch and the Wave D architect review;
  - invariant 11, scoped to the hold-expiry term. That follows the Wave D handover's "no new sweep-start upper bound" ruling, recorded in this ticket's Doing.
- **Guard duty:** no mechanical guard. `infrastructure.md` is outside check-catalog-claims' corpus, and the rest is prose that the docs build and the checkers cover.
- **Reported, not fixed:**
  - `infrastructure.md:843-845` overstates what a redacted record carries.
  - In `ci-cd.md`:
    - the DEV/PRO table says `Environment (default)`, but DEV runs in `dev-weu`;
    - the resource-group and app names are stale;
    - the Backend tip says "Both run with …";
    - the Secret scan's "fails it" is ambiguous.
  - CLAUDE.md's docs-table row sends request logging to `local-orchestration`.
  - The ADRs' Q-CI-01 mentions.
  - The trailing comments in the README's Jest block.
  - Stale `backend-ci.yml:69-71` and `:69-74` citations, which now land on the NuGet cache step (the unit tests are at `:87`). They are in security-rules, enforcement, patterns-backend, consistency and three ADRs (0043, 0049 and 0051).

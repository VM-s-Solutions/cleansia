# Cleanup Track — INDEX

The manifest for the 2026-08 cleanup. **Deliberately separate from `agents/archive/2026-08/backlog/`** — no ticket,
row or id is shared between the two tracks, so the state of this work is readable without untangling
it from three sprints of feature history.

Owner brief, 2026-08-12: resolve the open analysis findings, archive the feature backlog, make
`docs/` the source of truth, stop documenting inside the code, and walk every flow end to end for
gaps that actually matter.

## The three rules this track exists to obey

1. **One row per ticket, one status.** `agents/archive/2026-08/backlog/INDEX.md` records a ticket twice — a *filing*
   row and a *close-out* row with independent statuses — and on 2026-08-11 that sent four lanes at 24
   already-shipped tickets. There is no filing row here. This table is the only place a status lives.
2. **Cite the PR, never the branch SHA.** Every PR lands squashed, so a SHA recorded on a feature
   branch stops resolving the moment it merges. ~105 rows in the old backlog cite 18 dead SHAs for
   exactly this reason.
3. **A row is a claim about the past.** Before working a ticket, verify the thing it describes is
   still true in the tree. A `done` row is not evidence; the tree is.

## Convention

Ticket **files** are written at the start of their phase, not up front — writing a spec for P7 today
would be guessing at what the P1 flow walks will find. The row below is the commitment; the file is
the working spec, and it appears when the phase opens. Rows with no file yet are marked `todo`.

**Status:** `todo` · `in_progress` · `blocked` · `done` · `dropped`
**Size:** S (< half a day) · M · L

---

## P1 — Track, baseline, end-to-end analysis *(read-only against the codebase)*

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-001 | Cleanup track + INDEX scaffolding | S | done | #192 |
| CL-002 | Baseline census — build, checkers, LOC/comment/test counts | S | done | #192 |
| CL-003 | E2E walk — auth & identity | M | done | #192 |
| CL-004 | E2E walk — booking & pricing | M | done | #192 |
| CL-005 | E2E walk — payment & fiscal | M | done | #192 |
| CL-006 | E2E walk — offerability, preferred-cleaner hold, take | M | done | #192 |
| CL-007 | E2E walk — execution & completion | M | done | #192 |
| CL-008 | E2E walk — cancellation, refund, dispute | M | done | #192 |
| CL-009 | E2E walk — pay, pay periods, invoices, payouts | M | done | #192 |
| CL-010 | E2E walk — loyalty, memberships, metered benefits, referrals | M | done | #192 |
| CL-011 | E2E walk — GDPR, retention, audit, admin override | M | done | #192 |
| CL-012 | E2E walk — cross-cutting: tenancy, outbox, idempotency, notifications, rate limiting | M | done | #192 |
| CL-013 | Gap register — triage every finding into fix / accept / not-a-risk | M | done | #192 |

> **P1 complete.** All thirteen flows walked; findings triaged in `gap-register.md` — **2 to fix, 4 accepted-and-recorded, 19 checked and closed**. G-15's mechanism is decided: **seat ordinal** (owner, 2026-08-12).

## P2 — Fix wave

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-014 | Route the eight analysis findings to their owning phase | S | done | #193 |
| CL-015 | G-01 tenant stamping · G-15 seat ordinal (`MS-1` **cleared** — migration regenerated) | L | done | #193 |
| CL-016 | `C3` — 6 real teardown gaps fixed; 4 were checker false positives → CL-024 | S | done | #193 |
| CL-017 | `E6` — 13 sites converted to `collectAsStateWithLifecycle()` (checker saw 11) | S | done | #193 |
| CL-018 | Build warnings — 3 EF 10 deprecations + 24 xUnit analyser sites | S | done | #193 |

> **P2 complete.** 19 real defects fixed, 7 checker false positives routed to `CL-024`, 3,878 unit
> tests green. `CL-014` shrank on inspection: five of the eight findings are P9's work (README, root
> docs, dead scripts, `CLAUDE.md`) and one duplicated `CL-018`, so it became routing rather than fixes.
> **`MS-1` cleared** — `Initial` regenerated (`20260813085249`), the seat index exists, and the
> concurrency test passes against real Postgres. **DEV needs a database drop**: the migration id changed.

## P3 — Admin `errors.*` → `api.*` consolidation

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-019 | Two retirement assertions in `error-contract-parity.spec.ts` — no `errors` block, no reader | S | done | #194 |
| CL-020 | Repoint 30 files; merge 5 unique keys (164 of 169 were already duplicates) | M | done | #194 |
| CL-021 | Delete the `errors` block from all five admin locales; correct `CLAUDE.md` | S | done | #194 |

> **P3 complete.** The block was **97% redundant** — 164 of its 169 keys already existed under `api.*`,
> so this removed a duplicate rather than migrating a corpus. It also closed a live gap: `refund.failed`
> is a real `BusinessErrorMessage` that had **no** `api.*` translation, so any admin hitting it through
> the interceptor path saw the generic message. Admin locales: 235 `api` keys, no `errors`. 67 projects'
> tests green, admin app builds.

## P4 — Convention debt to a stated baseline

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-022 | Backend — 3 `B1` phantoms killed, `GetMyServingCleaners` → `IQuery`; `B3` ×21 + `B1` ×5 declared | M | done | #195 |
| CL-023 | Frontend + mobile — 16 phantoms killed (`: any`, `C3`, hardcoded `Text`); `D2` ×8 + `E1` ×9 declared | M | done | #195 |
| CL-024 | Fix `B10`, `C3`, `B1` and `conv` — 20 phantoms, each guarded by a new self-test | S | done | #195 |

> **P4 complete — and it inverted.** The phase assumed ~58 items of debt to clear; **20 of the 66 were
> the checker being wrong**, not the codebase. Those are fixed and guarded (self-tests 19 → 26, each
> narrowing paired with a "STILL flags" case). One real landmine was fixed: `GetMyServingCleaners` was
> an `ICommand` the UnitOfWork pipeline never commits, because it decides on the type-name suffix.
> The surviving **46 are declared with reasons** in `consistency-baseline.md` — none has a user-visible
> cost, and `B3` (21) and `D2` (8) need an owner ruling because both change working behaviour.

## P5 — Docs platform

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-025 | VitePress + mermaid; diagrams render, docs build clean | S | done | #196 |
| CL-026 | New IA — Product · Domain · Flows · Decisions, with real landing pages | M | done | #196 |
| CL-027 | `check-docs-refs.mjs` + 10 self-tests, wired as **Docs CI** (`T1-CI`) | M | done | #196 |

> **P5 complete.** Mermaid renders (client-side — a malformed diagram builds clean and breaks only in
> the browser, so diagrams get looked at). The IA gains Product · Domain · Flows · Decisions, each with
> a real landing page rather than a stub. `check-docs-refs.mjs` verifies both halves of a pointer —
> the page resolves AND the `#anchor` matches a heading — with 10 self-tests including two that prove
> it can fail. Wired as **`docs-ci.yml` — "Docs CI"** on owner instruction, joining Backend / Frontend
> / Android / iOS CI. It gates BOTH halves of the reference contract, neither of which was checked on
> a PR before: `code → docs` via the checker, and `docs → docs` via `vitepress build` — which enforces
> `ignoreDeadLinks:false` but previously ran only inside the deactivated, dispatch-only deploy
> workflow. The gate's own self-test runs first and blocks, so a change that defangs it reddens here.

## P6 — ADR migration

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-028 | Migrate **51** ADRs to `docs/decisions/adr-NNNN.md`; index + supersession graph | L | done | #197 |
| CL-029 | Archive 29 challenges + 7 drafts to `agents/archive/2026-08/adr-deliberation/` | S | done | #197 |

> **P6 complete.** **51** records, not 52 — the earlier count included `README.md`. Each is now
> `adr-NNNN.md`, so `/decisions/adr-0037` resolves and the ~618 citing source files needed no edit.
> 125 stale `agents/archive/2026-08/backlog/adr/` paths rewritten. Three defects surfaced on the way: a tool-call
> artifact committed inside ADR-0021, my own migration dropping frontmatter from the two YAML-style
> records, and two citations of the migration filename **P2 renamed** — which no gate caught, because
> `check-catalog-claims` runs in no workflow.

## P7 — Content build

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-030 | Domain model — ERDs per area, generated from the **61** EF configurations | L | done | #198 |
| CL-031 | Order lifecycle + offerability, both diagrammed | M | done | #198 |
| CL-032 | **Ten** flow pages — diagrams + edge-case tables incl. accepted residues | L | done | — |
| CL-033 | Business rules with rationale + the feature list by audience | L | done | #198, — |
| CL-034 | Split `agents/knowledge/` by audience — 20 files published, corpus follows them | M | done | — |

> **P7 content complete except `CL-034`.** Landed: the domain model (61 entities, ERDs per area,
> generated from the EF configurations rather than described), the two-axis order lifecycle, offerability
> including the take cascade, and the business rules with their rationale. Outstanding: `CL-032` (the
> ten flow pages), `CL-034` (the `agents/knowledge/` split), and the product feature list half of
> `CL-033`. Four `L`/`M` tickets is more than one reviewable diff.
>
> **P7b** landed the ten flow pages and the feature list. **P7c** closed `CL-034`: the S1–S12 security
> laws, 18 component contracts and the expandability doctrine published; the pattern catalogues,
> `consistency.md`, `conventions.md`, `testing.md` and `runtime-readiness.md` stay as build instruction.
> 86 files' references rewritten. `check-catalog-claims` reads **the same 36 corpus files** it always
> did — the paths moved with the files rather than the corpus widening to `docs/**`, which would have
> swept in 51 ADRs whose citations have never been C3-checked.

## P8 — Comment migration

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-035 | C# comment migration — 76 files | L | done | #201 |
| CL-036 | Android 81 · iOS 5 · Angular 10 | L | done | #201 |
| CL-037 | The rule in `conventions.md` + the docs charter | M | done | #200 |

> **P8, all four stacks.** 187 files, **−1,419 net lines**, 21 commits, 181 live pointers, both gates
> green by exit code. The rule that decided every block: *if a reader deleted this line and changed the
> code, would they break something?* Yes → it stays where the code is. Only explains why → it moves to
> the docs site and leaves a `→ /path#anchor`.
>
> **Comment ratio was not the signal.** `IOrderRepository` sat at 69 % comments and almost all of it was
> warnings — it barely moved. The volume came from worked examples and restated arithmetic.
>
> **Two mechanical approaches failed and were reverted whole**, which is the finding worth keeping:
> a sentence-selector mangled 71 KDoc blocks, and offset arithmetic computed against a batch-1 tree but
> applied to the original corrupted 20 Kotlin files (`Expecting a top level declaration`). What worked,
> and what any future sweep should use: **text-anchored replacement, pre-flight assertion that each
> anchor matches exactly once, a character-level `/* */` scanner over every touched file, and both gates
> read by exit code before the commit** — not by eyeballing their output.

## P9 — Archive & delete

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-038 | Archive the backlog → `agents/archive/2026-08/backlog/` (kept in git) | M | done | #202 |
| CL-039 | Delete the dead — `_legacy/`, root `planning/`, six frozen root docs, 28 spent wave scripts, the `Infra.Scripts` project | M | done | #202 |
| CL-040 | Rewrite `README.md` — it handed out `Add-Migration` against paths that do not exist | S | done | #202 |
| CL-041 | Slim `CLAUDE.md` to the working agreement + pointers into the docs site | M | done | #202 |

> **P9 complete. 602 files, −30,500 net lines.** `README.md` 15 → 95 lines (it was Visual-Studio-era
> scratch notes pointing at `03 Infrastructure\` and `05 Web\`, folders that have never existed in this
> layout). `CLAUDE.md` 594 → 220: the working agreement is kept **verbatim**, and everything that
> explained the domain is now a pointer, leaving only what an agent must *do* or must *not* do — the
> four landmines, the manual-steps prohibition, the conventions.
>
> **Two of the four rows misdescribed the tree, which is rule 3 earning its place.** `agents/planning/`
> does not exist and never did — it is `planning/` at the repo root. And `Infra.Scripts` is not an
> "empty project": it compiles zero `.cs` files, but it carried **19 seed SQL scripts, 18 of them the
> only copy in the repo**. Deleting the folder as the row instructed would have destroyed them. They
> are now `sql-scripts/seed/` with a README.
>
> **And the 19th nearly shipped a silent break.** `insert_seed_data.sql` looked like a byte-identical
> duplicate of the repo-root copy, so it was deleted as redundant — but the duplication was
> deliberate: `CleansiaStartupBase.SeedDevelopmentData` resolves *that* path from the solution
> directory and executes it, so a fresh Development boot would have logged *"Seed file not found.
> Skipping seed."* and carried on with an empty database and no failure. It was caught by
> `StartupSeedScriptSyncTests`, a pin written after the two copies drifted once before. Restoring the
> duplicate was the wrong repair: startup now reads `sql-scripts/insert_seed_data.sql`, already the
> file three other test classes read, so **one copy exists and the drift class is gone rather than
> policed** — and the pin retires with it.
>
> Archiving the backlog was not a `git mv` either: **~404 references across 170 files point into it,
> 31 of them published ADRs.** Those were rewritten in the same commit, or P7's reference contract
> would have broken the moment the folder moved. `check-backlog-consistency.mjs` built its path from
> string segments rather than a literal, so the prose rewrite missed it and the checker went red on the
> next run — it now reads the archive and guards that history against being edited into disagreeing
> with itself.
>
> **Correction, 2026-08-13 (P10).** The paragraph above is wrong in exactly the way this track exists
> to catch. P9 rewrote every `agents/backlog/` string it found, which made dead paths *resolve* — and
> resolving to the wrong place is worse than breaking, because nothing complains. **22 instructions
> across 10 live files were left telling agents to WRITE into frozen history**, including step 1 of
> `/feature` (`.claude/agents/pm.md:27`). A separate **63 links across 13 files** pointed at
> `backlog/adr/…`, a path that has existed nowhere since P6 — untouched, because they do not contain
> the string the rewrite searched for. Both closed by `CL-042`.

---

## P10 — Loose ends

The nine phases each wrote sentences that were true when written. This phase checks which of them
still are. Every row was ground-truthed against the tree first; **29 of 60 candidates were refuted**
under adversarial verification.

The finding that frames the rest: **`docs/` was declared the source of truth, but only the ~100 pages
this track authored were ever verified.** 24 pages under `docs/{api,deployment,customer-app,partner-app,admin-app}`
were written in **April 2026** and promoted to authoritative without a read. `CL-045`–`CL-048` are
what that promotion bought.

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-044 | `Web.Partner`'s anonymous health endpoint returned `ex.Message` to the public internet | S | done | #203 |
| CL-042 | 63 dead `backlog/adr/` links; 22 instructions telling agents to write into frozen history | M | done | #203 |
| CL-045 | `docs/customer-app/authentication.md` documents a bearer/localStorage session the tree replaced | M | done | #203 |
| CL-049 | Gate holes — `check-docs-refs` passes having read zero files; Backend CI blind to `sql-scripts/` | M | done | #203 |
| CL-047 | Three P7 pages carry domain claims the tree contradicts | M | done | #203 |
| CL-046 | `platform-expandability.md` still orders a catalog-tenancy migration that shipped in June | M | done | #203 |
| CL-048 | Operator pages — two config keys that bind nothing, a CI inventory missing five of ten | S | done | #203 |
| CL-050 | iOS — eight live files still call the first client generation owner-gated; it shipped | S | done | #203 |
| CL-052 | A dead 299-line `PayCalculator`, and three comments asserting constraints the tree lifted | S | done | #203 |
| CL-043 | `MANUAL_STEPS.md § Open` reads *(none)* while two owner actions are owed | S | done | #203 |
| CL-051 | ADR-0030 records as OPEN two gates the tree closed — dated correction note | S | done | #203 |

### Handed over — and then worked, on the owner's word (2026-08-14)

Filed as decisions, six of eight came back with a ruling and were built in the same PR.

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-053 | Old backlog **deleted**; `agents/backlog/` is live again, one row per ticket | M | done | #203 |
| CL-055 | `IsUniqueViolationOn` + `DbConstraintNames` deleted — *"it's over engineering, remove it"* | S | done | #203 |
| CL-060 | G-11 — the admin entry-instruction read is now an audited Command | M | done | #203 |
| CL-057 | Admin pay-period close no longer confirms in hardcoded English | S | done | #203 |
| CL-056 | Dead camera affordance removed; the dispute author label is reported, not decided | S | done | #203 |
| CL-054 | `CurrencyCode` on the pay summary + the dashboard card; regen is `MS-4` | M | done | #203 |
| CL-058 | `B3` ×21 — the RULE was wrong; narrowed with a paired self-test, sites stand | M | done | #203 |
| CL-059 | `D2` ×8 — converted; the "behaviour change" the baseline claimed did not exist | M | done | #203 |
| CL-061 | Admin reveal control — shipped; `MS-5` cleared by the owner mid-PR | S | done | #203 |
| CL-062 | The three product calls: dashboard chart currencies · dispute author label · stale ADR paths | M | done | #203 |

> **`CL-060` shrank on inspection, which is rule 3 again.** It was filed as a four-platform feature
> (admin web, partner web, Android, iOS). The *assigned* cleaner's access is correct on every platform
> and unchanged, so the whole defect was one admin read — backend-only. And `T-0483`, the draft ticket
> the gap register worried would disappear into the archive, turned out to have **no ticket file at
> all**; it was only ever an INDEX row.
>
> **`CL-058` and `CL-059` both resolved against the tree, and in `B3`'s case the checker was the
> defect.** Its 21 sites are three populations: 5 inherit a base that declares **no constructor rules**
> — only `protected` helpers the derived class calls, behaviourally identical to inlining; 5 inherit
> `LoginValidator`, whose ordering is the deliberate point; and 11 inherit `UserEmailValidator`, whose
> constructor rule re-checks the caller against the database on every request. **That last one is
> load-bearing**: the three web hosts install no revocation directory, a Partner access token lives
> 1440 minutes, and GDPR erasure rewrites `User.Email`, so it is the only thing stopping an erased or
> unconfirmed principal acting on a still-valid token. Owner confirmed the intent. The rule now exempts
> the four bases by name with a paired *"STILL flags anything else"* self-test.
>
> `D2`'s ×8 were converted, because the risk the baseline stated was not there: 2 are no-ops (those
> controls already declare `nonNullable: true`), 5 of the remaining 6 forms never call `reset()`, and
> the one that does resets `''` instead of `null` — indistinguishable to both the user and
> `Validators.required`. **`check-consistency` drops from 44 to 15, and almost all of that is the
> checker being wrong rather than the codebase improving.**

## P11 — Verifying P10

P10 verified P1–P9 and was itself never verified. Six lenses swept **its own diff**: 19 candidates,
**10 refuted**, and six rows survived — **none of them code**.

**Nothing P10 changed is wrong.** Every behavioural change verified correct against the tree: the
withheld `AccessInstructions`, the audited reveal Command, the currency threading on four platforms,
the `B3` narrowing and its paired self-test, the deleted `PayCalculator`, the deleted
`IsUniqueViolationOn`. What P10 left behind is the **other half of the sentence** — a correction made
in one place while the same claim survived elsewhere on the page, or in the map at the top of the repo.

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-063 | `Cleansia.Api.slnx` named a project P9 deleted — that solution could not restore | S | done | #204 |
| CL-068 | The review gate's hard-fail discriminator named a deleted file; 7 live sites cited it | S | done | #204 |
| CL-066 | A flow page called a shipped, audited control an accepted privacy hole | S | done | #204 |
| CL-064 | `CLAUDE.md` § Trackers: a dead path, a closed manifest called *live*, the live backlog unnamed | S | done | #204 |
| CL-065 | `ci-cd.md` still documented a DEV auto-deploy and a typed-`deploy` PRO gate; both are gone | M | done | #204 |
| CL-067 | `api/authentication.md`'s first table taught the lifetimes the same page calls phantom | S | done | #204 |
| CL-069 | `enforcement.md`'s "44 declared violations" — count removed, not reset | S | done | #204 |
| CL-070 | `partner-app/dashboard.md` documented the hardcoded suffix P10 replaced | S | done | #204 |
| CL-071 | The twelve `conv` narrowings had no regression test — and pinning them disproved the risk | S | done | #204 |

> **The two with real teeth.** `CL-063`: there are **two** solution manifests in `src/`, and P9 removed
> `Infra.Scripts` from only one — `dotnet restore Cleansia.Api.slnx` hard-failed `MSB3202` on a project
> deleted on purpose. CI never noticed because every workflow names the `.sln` explicitly. `CL-068`: the
> reviewer charter makes "a violation not in `backlog/audits/consistency-violations.md`" a **hard fail**,
> and that file no longer exists — so with 15 live declared violations, any PR touching those files
> tripped a gate with no way to show the violation was baselined. Seven live sites cited it; the
> replacement, `consistency-baseline.md`, was linked from exactly one.
>
> **`CL-066` is the one worth remembering.** `OrderPiiRedaction.cs` — the file that *implements* the
> withholding — carries a `→` pointer to a page that said every admin could read entry instructions
> with no audit record, and that this was *deliberately accepted*. A security file arguing against its
> own control. That is what a half-finished correction costs once `docs/` is the source of truth.
>
> **Two were dropped on proportionality and the owner overrode both (2026-08-15: *"do all of the tasks
> that the investigation found"*).** They are `CL-069` and `CL-070`, and doing them was cheap enough
> that the override costs nothing to honour.
>
> `CL-069` is the more interesting of the two. The fix is **not** to change 44 to 15: the file's own
> §*"A claim about the tree carries its own retirement condition"* names this decay class, and its iOS
> row already records being burned by it twice in one afternoon. So the count came **out**, with the
> shape rule stated — *never enumerate a count of tree instances* — and a pointer to
> `consistency-baseline.md`, which is allowed to hold the number because it is the thing being counted.

## P12 — The two latent risks

Not archaeology. `G-03` and `G-18` were **accepted** by the P1 analysis with a documentation
obligation, which P7 discharged; the code half was deferred because both needed a schema change. The
owner chose to close them on 2026-08-15, and the timing is the point — `MS-2` already owed a
regenerated `Initial` and a DEV drop, so the migration both were waiting on had become free.

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-072 | `G-03` persist `rememberMe` · `G-18` make the recurring occurrence key unique | M | done | #205 |

> **`G-18` is the one with money attached.** The materializer decides "did we already spawn this
> occurrence" with an unlocked read, and nothing enforced the answer — what prevented a duplicate order,
> and on a card template a duplicate **charge**, was that Azure Functions timer triggers hold a singleton
> lease. That is a guarantee in the **hosting model**, not the schema: move the sweep to another
> scheduler or fan it out and duplicate billing returns with nothing to catch it. Now a unique filtered
> index over `(RecurringTemplateId, CleaningDateTime)` is the arbiter, and the failure becomes one
> background tick that fails and self-heals.
>
> **`G-03` was silent rather than expensive.** Rotation inferred `rememberMe` by measuring
> `ExpiresAt - CreatedOn` against `RefreshTokenShortExpDays + 0.5` — correct for every shipped config,
> but it couples a security property to the *gap* between two independently-tunable numbers. Configure
> them within half a day of each other and every session quietly becomes short-lived. The flag is now
> stored; the arithmetic survives only as the fallback for rows predating the column, which self-heal.
>
> **The migration is regenerated and both are live.** `20260813085249_Initial` →
> `20260815094107_Initial`, verified by **197 integration tests against real Postgres** — which is the
> only thing that proves the model and the schema agree, and is why the unit suite passing meant
> nothing here. Backend CI was briefly red with
> `42703: column "RememberMe" of relation "RefreshTokens" does not exist`, the same failure `MS-1`
> produced for `SeatOrdinal` in P2.
>
> **And the rule moved.** Regenerating `Initial` was owner-only; the owner made it an agent step on
> 2026-08-15. `CLAUDE.md` § *Manual steps* now carries the commands and the trap that the startup
> project must be a web host, because `Cleansia.MigrationService` does not reference
> `Microsoft.EntityFrameworkCore.Design` and the tool refuses it. **The DEV drop stays the owner's** —
> the id changed again, so `MS-2` is now owed against `20260815094107`.

---

## Out of scope — named, so it stays out

- **No EF migration, no NSwag regen.** Owner-only; a phase that needs one raises a `MANUAL_STEP`.
- **No UI work** on any of the five apps.
- **No re-litigating shipped ADRs** during migration. They move as written. An ADR that contradicts
  the tree is reported as a finding, not silently edited.
- **Not documenting what OpenAPI already generates.**
- `Address.State` stays — it is for US/CA launch.
- `CHANGELOG.md` stays a repo-root file and is deliberately not moved into the site.
- **No attribution to Claude** on any commit, PR or page — see `CLAUDE.md` § *Conventions Summary*.

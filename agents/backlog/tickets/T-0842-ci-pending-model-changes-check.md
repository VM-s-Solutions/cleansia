---
id: T-0842
title: Fail backend CI when the EF model has changes the Initial migration does not carry
size: XS
owner: pm
created: 2026-10-12
updated: 2026-10-13
depends_on: []
blocks: []
stories: []
adrs: []
layers: [ci, db]
security_touching: false
---

## Context

The 2026-10-12 design ruling on BE-11 to BE-14 reported this as item 5. The ruling is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/architect_be-lead.json`.

Schema changes are folded into the one `Initial` migration by regenerating it (CLAUDE.md, *Database migrations*). Nothing in CI notices a model change that lands without that regeneration. Lines are as of `207d2f319`.

- **The tests ignore pending model changes.**
  - Both test fixtures ignore `PendingModelChangesWarning` (`BaseIntegrationTest.cs:89`, `HostTestPostgresFixture.cs:88`), and so do two tests (`AppLoginGrantScriptTests.cs:130`, `DevelopmentFirstBootLegalSeedTests.cs:65`).
  - The fixtures' comments (`BaseIntegrationTest.cs:82-87`, `HostTestPostgresFixture.cs:83-87`) say the model "can sit slightly ahead" of the migration, "a snapshot the owner regenerates manually". Their example is the `RefreshToken` `xmin` token.
  - Both claims are out of date: that token is in the snapshot now (`CleansiaDbContextModelSnapshot.cs:2832`), and regeneration has been ordinary work since 2026-08-15.
- **The migrator applies only pending migrations** (`Cleansia.MigrationService/Program.cs:31-39`). So a database already at the current id is never told that the model moved.
- **CI runs no EF check.** backend-ci (`.github/workflows/backend-ci.yml:77-111`) restores, builds and runs the three suites. It runs no `dotnet ef` command, and the repo has no `dotnet-tools.json`.
- **Open:** whether a change that only adds a concurrency token is detected at all. T-0844's token emits no DDL, only snapshot lines.

**If not built:** a forgotten regeneration passes CI. It is found later, when a fresh database disagrees with the model; for a change with no DDL, it may never be found.

## Doing — approved 2026-10-13

1. **Check today's state first.** Run `dotnet ef migrations has-pending-model-changes` at HEAD to learn whether the model and the snapshot agree. If they do not, this change waits for a regeneration.
2. **Stop suppressing the warning** in the two fixtures and two individual tests; remove the two stale fixture comments. No new tool, package, workflow step or snapshot test.
3. **Prove the fixture boundary.** With an isolated scratch column addition without regenerating, the check goes RED. After restoring the isolated model state it goes GREEN; real model changes join the wave’s one Initial regeneration. Record whether a change that only adds a token is caught.
4. **Keep the required check's name.** No `build` job gains `name:`, `strategy:`, `if:` or `uses:`.
5. **Docs.** `docs/deployment/ci-cd.md` says what backend-ci now checks.

## NOT doing

- No change to how the fixtures build their databases beyond the warning, and no change to the migrator.

## Acceptance criteria (done looks like)

- [ ] **AC1** — A model change without a regenerated `Initial` fails backend CI's `build`. This is shown RED then GREEN with an isolated scratch mutation and recorded here.
- [ ] **AC2** — Whether a change that only adds a token is caught is recorded here.
- [ ] **AC3** — The required check keeps its name, and the full suites pass.

## Owner decision — 2026-10-13

Mike, answer 8: "Analyze and Fix those".

Mike, default choice: "Go with defaults (recommended)".

Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

The chosen XS shape removes the four PendingModelChangesWarning suppressions and the two stale fixture comments. No CI tool step, package or snapshot-text test is added. Existing unsuppressed seed-migration coverage already catches relational drift; a token-only metadata change is not caught and causes no DDL difference. Measure the current model, prove a real DDL mutation refused by the fixtures, and restore the scratch bytes. The migrator does not check relational drift on a startup with no pending migration ID.

Evidence root: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/`. Source checks are pre-implementation findings; no test, implementation review, CI or deployment result is asserted by this approval record.

## Work record

- 2026-10-12 — Filed from the BE-11 to BE-14 design ruling's reported item 5. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

## Review

Pending.

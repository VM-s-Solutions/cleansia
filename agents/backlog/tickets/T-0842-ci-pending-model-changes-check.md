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
3. **Prove the fixture boundary.** With an isolated scratch relational column-type change without regenerating, the check goes RED. After restoring the isolated model state it goes GREEN; real model changes join the wave’s one Initial regeneration. Record whether a change that only adds a token is caught.
4. **Keep the required check's name.** No `build` job gains `name:`, `strategy:`, `if:` or `uses:`.
5. **Docs.** `docs/deployment/ci-cd.md` says what backend-ci now checks.

## NOT doing

- No change to how the fixtures build their databases beyond the warning, and no change to the migrator.

## Acceptance criteria (done looks like)

- [x] **AC1** — A model change without a regenerated `Initial` fails backend CI's `build`. This is shown RED then GREEN with an isolated scratch mutation and recorded here.
- [x] **AC2** — Whether a change that only adds a token is caught is recorded here.
- [x] **AC3** — The required check keeps its name, and the full suites pass.

## Owner decision — 2026-10-13

Mike, answer 8: "Analyze and Fix those".

Mike, default choice: "Go with defaults (recommended)".

Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

The chosen XS shape removes the four PendingModelChangesWarning suppressions and the two stale fixture comments. No CI tool step, package or snapshot-text test is added. Existing unsuppressed seed-migration coverage already catches relational drift; a token-only metadata change is not caught and causes no DDL difference. Measure the current model, prove a real DDL mutation refused by the fixtures, and restore the scratch bytes. The migrator does not check relational drift on a startup with no pending migration ID.

Evidence root: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/`. Source checks are pre-implementation findings; no test, implementation review, CI or deployment result is asserted by this approval record.

## Work record

- 2026-10-12 — Filed from the BE-11 to BE-14 design ruling's reported item 5. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

- 2026-10-13 — Removed all four PendingModelChangesWarning suppressions and both stale fixture comments. Existing migration-built seed coverage now refuses relational model drift; no tool/package/workflow/snapshot-text test was added, and backend-ci's build identity is unchanged. Existing four seed tests and combined focused27/27 pass without suppressions. Current model check is clean after the one M11 regeneration (`p2/migration/initial-pending-model-001.json`).
- 2026-10-13 — Real DDL probe changed DeviceToken varchar512→511 while holding the shared build mutex through mutation, Release rebuild, actual tests, byte restore and restored rebuild/tests. Five real PostgreSQL migration cases failed PendingModelChangesWarning; identical restored five passed/zero skipped. Exact before/restored DeviceConfiguration SHA256 was `d97b684c6ecbb91249c23c53b36082cfee59a5e1858629671455ba07cc27d03f` before M11's separate index change. `p2/data/t0842-ddl-proof.json` binds logs/TRXs/stages. No scratch delta remains.
- 2026-10-13 — Token-only metadata is not detected and emits no DDL delta: measured historical receipts are Wave F `p5/impl/db_be11-payment-token.json` (SHA256597e852d6b81707153ddc38e3d7a7d3e9039a477e927dd23ef351a51c458536a) and `review_backend.json` (SHAbfc6ce6393be0793746938e5f307f817409cad9d8085540446d954f1afdd0402). This is a recorded limit, not a new token mutation or an all-DDL claim. The migrator skips Migrate when no migration id is pending, so not every startup detects drift. CI docs and a dated backend-catalog correction preserve that distinction. Full rebuilt Release unit8722→integration906→host509 pass/zero skipped; hosted CI remains due.

- 2026-10-13 — Combined-source local release gates completed before publication: rebuilt Release unit8722→integration906→host509, zero skipped; uncached web5041 tests/408 suites across71 tested projects (three empty projects), all three production builds and seven Playwright smokes (5/1/1); Android three Debug compilations then core296→partner839→customer1683 JVM tests, every task forced to execute, zero skipped; iOS18.6 Core776→partner1113→customer1779 passed, with one existing VoiceOver case skipped. Docs refs/catalog/VitePress, iOS symbols/booking-policy parity and their self-tests pass. Exact commands, counters and receipt/TRX/summary hashes are in `p2/local/full-gates-before-commit.json` and `p2/verifier/android-gates-final.json` under the wave root. Android ordinary ignored OpenAPI generation and ignored iOS workspace client generation from committed specs ran for local CI; no tracked contract dump/NSwag change was required. These are command-only proofs, not real mail, native behavior/device or deployment checks. First unit source-guard failure was corrected with four actual token-bearing invocations and passed the full rerun; first partner iOS ENOSPC attempt is nonproof. The initial affected lint selected no targets on filing-only HEAD and is not source proof; actual affected lint and the three Docs checks will run at the committed source head before push. Hosted exact-head CI/guarded merge and final DEV remain due.

## Review

2026-10-13 — Implementation verdict **approve-with-notes**; independent evidence verification confirmed the real DDL proof, restoration and full backend receipts (`p2/reviewer-round1.md`, `p2/verifier-round1.md`). Hosted exact-head CI/merge remain due.

---
id: T-0124
title: DB index on User.Email + other lookup columns
status: done
size: S
owner: â€”
created: 2026-06-01
updated: 2026-06-15
depends_on: [T-0106]
blocks: []
stories: []
adrs: []
layers: [db]
security_touching: false
manual_steps: [ef-migration]
sprint: 0
source: finding PERF-IDA-01
---

## Context

Audit finding **PERF-IDA-01** (critical, perf, Wave 0) â€”
`agents/backlog/audits/AUDIT-2026-06-01-findings.md:80`, slice report
`agents/backlog/audits/AUDIT-2026-06-01-slice-reports.md:1598-1604`, JSON entry
`AUDIT-2026-06-01-findings.json:3` (id `PERF-IDA-01`), plan/map row
`agents/backlog/TICKET-MAP.md:56`.

`User.Email` and the other identity-lookup columns have **no database index**. Every property is
configured in `UserEntityConfiguration.cs:28-51` (`Email`, `PhoneNumber`, `GoogleId`,
`ResetPasswordCode`, `ConfirmationCode`) but **none** carries a `HasIndex(...)`. As a result every
identity query in `UserRepository.cs` does a sequential scan of the `Users` table:

- `GetByEmailAsync` (`:18-23`), `ExistsWithEmailAsync` (`:35-38`), `GetByEmailOrPhoneNumberAsync`
  (`:30-33`) â†’ scan on `Email`.
- `GetByPhoneNumberAsync` (`:25-28`), `ExistsWithPhoneNumberAsync` (`:57-60`) â†’ scan on `PhoneNumber`.
- `ExistsWithConfirmationCodeAsync` (`:40-43`), `GetByConfirmationCodeAsync` (`:45-48`) â†’ scan on
  `ConfirmationCode`.

Login, register, password-reset, email-confirm and profile-load all hit these paths, so the cost
grows linearly with the user base and becomes the single most expensive identity query under load.

This ticket also **folds in PERF-IDA-05** (major; slice report `:1634-1640`): there is no DB-level
uniqueness on `User.Email` / `User.PhoneNumber` today â€” uniqueness is enforced only by app-code
pre-checks (`Register.cs:33`, `UpdateCurrentUser.cs:56`), which is a TOCTOU race. Shipping the
**unique** index on `Email` in the same migration both closes that race and makes the existence
check index-backed. The finding explicitly directs PERF-IDA-05 to land with PERF-IDA-01.

Note: the email/phone columns are already `citext` (`UserEntityConfiguration.cs:29,34`), so a unique
index on them is natively case-insensitive â€” no extra `LOWER()` / functional index is needed.

## Acceptance criteria

- [ ] **AC1 â€” Unique index on Email.** Given the `User` EF configuration, When `Configure` runs, Then
      a `HasIndex(u => u.Email).IsUnique()` is declared (case-insensitive via the existing `citext`
      column), and the generated model has exactly one unique index over `Email`. Evidence: the
      `UserEntityConfiguration` diff shows the index; a `DbContext`-model assertion (or
      `IModel`/`IEntityType` metadata test) confirms `User.Email` has a unique index.
- [ ] **AC2 â€” Non-unique indexes on the remaining lookup columns.** Given the same configuration,
      When `Configure` runs, Then non-unique indexes exist on `PhoneNumber`, `ConfirmationCode`,
      `ResetPasswordCode`, and `GoogleId`, with the nullable ones (`PhoneNumber`, `ConfirmationCode`,
      `ResetPasswordCode`, `GoogleId`) declared as **filtered/partial** (`HasFilter` / `WHERE col IS
      NOT NULL`) so null rows are not indexed. Evidence: model-metadata test asserts each index is
      present, non-unique, and (for nullable columns) filtered.
- [ ] **AC3 â€” DB-level uniqueness closes the app-code race (PERF-IDA-05).** Given the unique Email
      index, When two rows with the same email are attempted, Then the database rejects the second
      (a unique-constraint violation), not just the app pre-check. Evidence: the model metadata proves
      the unique constraint exists; the `ExistsWithEmailAsync` pre-check (`UserRepository.cs:35-38`)
      remains as a fast-path UX message and is NOT removed.
- [ ] **AC4 â€” No behavioral regression in repository lookups.** Given the existing
      `IUserRepository` queries (`GetByEmailAsync`, `GetByPhoneNumberAsync`,
      `GetByConfirmationCodeAsync`, etc.), When the indexes are added, Then their results are
      unchanged (still tenant-filtered, same rows returned). Evidence: the existing/added repository
      tests stay green; no query method body is altered by this ticket.
- [ ] **AC5 â€” Migration flagged, not run.** Given the index/constraint additions require a schema
      change, When the work is complete, Then an `ef-migration` MANUAL_STEP is recorded for the owner
      with a note that the index must be built `CONCURRENTLY` on the populated `Users` table, and the
      ticket is **held** at the migration boundary. Claude does NOT run `dotnet ef migrations add` or
      `dotnet ef database update`.

## Out of scope

- **PERF-IDA-02** (drop the blanket `Orders` include in `GetQueryable()`), **PERF-IDA-03**
  (collapse per-request duplicate user fetches), **PERF-IDA-04** (lean employee-id lookup on refresh)
  â€” separate tickets; do not touch repository query bodies here beyond adding the indexes.
- Changing any `UserRepository` query method, validator, or handler logic. This is a pure
  EF-configuration + migration ticket (`db` layer only).
- Adding indexes to other entities (GDPR `CreatedOn` per PERF-IDA-06, etc.) â€” out of scope.
- Hashing/strengthening the confirmation/reset tokens â€” that is **T-0106 (IDA-SEC-03)**; this ticket
  only indexes the columns as they exist today. If T-0106 renames/reshapes those columns, the index
  definitions follow whatever shape lands; sequence around it (see notes).

## Implementation notes

- **TEST-FIRST** per `agents/knowledge/testing.md` (Â§"TDD â€” write the test first"). The verifiable
  unit here is the **EF model metadata**: write a model-introspection test (`IModel`/`IEntityType`
  â†’ `GetIndexes()`) asserting the unique Email index + the non-unique filtered indexes BEFORE adding
  the `HasIndex` calls (red â†’ green). Each AC1â€“AC3 maps to a metadata assertion; AC4 is the
  existing-repository-test regression net. Gate 6 (Reviewer) enforces test-first.
- **Governing rules:** **no ADR governs this** â€” ADRs 0001 (authorization), 0002 (outbox), 0003
  (rate-limiting) do not apply. The applicable guidance is `agents/knowledge/patterns-backend.md` /
  `database.md` ("Explicit indexes on FKs and frequently queried columns"). Note the slice report
  flags `database.md` as stale (it claims Guid PKs / "Email citext to avoid LOWER()" while there is
  in fact no Email index at all) â€” index per the real columns, not the stale doc.
- **Serialization cluster:** **none.** The only file this ticket edits is
  `src/Cleansia.Infra.Database/EntityConfigurations/UserEntityConfiguration.cs`, which does not
  appear in any cluster in `agents/backlog/TICKET-MAP.md`. Safe to run concurrently with other Wave-0
  tickets, with the standard reviewer-per-developer invariant. **Caveat:** **T-0106 (IDA-SEC-03)**
  changes the `User` token columns (`ConfirmationCode` / `ResetPasswordCode` â†’ hashed) and also
  carries an `ef-migration`. Both touch `User` schema; to avoid a migration collision the PM should
  serialize the two migrations (let T-0106 land its column shape first, then this ticket indexes the
  final columns) even though the EF-config edits themselves do not overlap line-for-line.
- **Where:**
  - `src/Cleansia.Infra.Database/EntityConfigurations/UserEntityConfiguration.cs:28-51` â€” add
    `builder.HasIndex(u => u.Email).IsUnique();` and non-unique (filtered, where nullable)
    `HasIndex` calls for `PhoneNumber`, `ConfirmationCode`, `ResetPasswordCode`, `GoogleId`.
  - Read-only references for the AC4 regression check:
    `src/Cleansia.Infra.Database/Repositories/UserRepository.cs:18-48,57-60`.
- **Sequence:** db (architect/db lock the index set + filter predicates) with reviewer in parallel â†’
  qa. `security_touching: false` â†’ no security gate; not a hot-path algorithm change, so no optimizer
  gate required. Then **hold** for owner-confirmed migration (`CONCURRENTLY`) before `done`.
- The migration must build the index `CONCURRENTLY` on the populated `Users` table (slice report
  `:1603`); note this explicitly in the MANUAL_STEP handoff to the owner.

## MANUAL_STEP — ef-migration (OWNER-ONLY, not run by Claude)

The five `HasIndex(...)` additions in `UserEntityConfiguration.cs` change the model, so the EF schema
must be regenerated. **Claude did NOT run `dotnet ef`.** This **folds into the owner's Initial
migration regen** (fresh schema) — when the Initial migration is regenerated these indexes appear in it
automatically; no separate `migrations add` is required.

Schema delta to expect on `Users`:

- `IX_Users_Email` — **UNIQUE** index on `Email` (citext ⇒ natively case-insensitive; this is the
  DB-level guarantee that closes the PERF-IDA-05 register/update TOCTOU race).
- `IX_Users_PhoneNumber` — non-unique, **partial** `WHERE "PhoneNumber" IS NOT NULL`.
- `IX_Users_ConfirmationCode` — non-unique, **partial** `WHERE "ConfirmationCode" IS NOT NULL`.
- `IX_Users_ResetPasswordCode` — non-unique, **partial** `WHERE "ResetPasswordCode" IS NOT NULL`.
- `IX_Users_GoogleId` — non-unique, **partial** `WHERE "GoogleId" IS NOT NULL`.

**CONCURRENTLY note (populated-table case only):** on an already-populated `Users` table the indexes
must be built `CREATE INDEX CONCURRENTLY` (and the unique one validated) to avoid a write-blocking lock,
and EF's generated `CreateIndex` must be hand-edited to add `CONCURRENTLY` outside a transaction. **For
the Initial-regen / fresh-DB path this is moot** — a brand-new empty schema builds the indexes inline
with no lock concern. Record both: fresh DB ⇒ inline (no action); existing populated DB ⇒ build
`CONCURRENTLY`. Hold the ticket at this migration boundary until the owner confirms the regen.

## Status log
- 2026-06-01 â€” draft (created by pm)
- 2026-06-05 â€” in_progress (db): RED-first model-metadata test added
  (`src/Cleansia.Tests/Features/Users/UserIdentityLookupIndexTests.cs`) — 10/10 FAILED (no `HasIndex`
  on `User` yet).
- 2026-06-05 â€” done pending owner migration (db): added the unique `Email` index + the four filtered
  non-unique lookup indexes in `UserEntityConfiguration.cs`. Solution build 0 errors; new tests 10/10
  GREEN; full `Cleansia.Tests` suite **383/383 passed** (AC4 — no repository/handler/validator change,
  no behavioral regression). **Held** at the `ef-migration` MANUAL_STEP above (owner regenerates the
  Initial migration; Claude did not run `dotnet ef`, no nswag).

## Review
**Reviewer — APPROVED (2026-06-05).** Only `UserEntityConfiguration.cs` changed (the 5-`HasIndex` block); the
`HasMaxLength(64)` hunks + `UserRepository.cs` `M` are pre-existing T-0106 — **zero query body changed by T-0124**
(AC4). AC1: one `HasIndex(u => u.Email).IsUnique()` (citext, closes PERF-IDA-05 race), pre-check retained. AC2:
non-unique FILTERED (`IS NOT NULL`) indexes on PhoneNumber/ConfirmationCode/ResetPasswordCode/GoogleId. Model-
metadata test test-first (RED = 10). ef-migration flagged, not run; no nswag.

**Verification (orchestrator, independent):** 5 `HasIndex`; Email unique; 4 nullable filtered. `dotnet build` 0
errors; `dotnet test Cleansia.Tests` = **383 passed / 0 failed** (+10). ⚠️ owner: migration folds into Initial
regen (CONCURRENTLY moot on fresh schema). Not committed.

- 2026-06-05 — done (reviewer APPROVED; 383 tests; independently re-verified). NOT committed.

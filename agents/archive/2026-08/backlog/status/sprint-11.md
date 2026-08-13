# Sprint 11 — WAVE 9: Admin Action Audit Log (+ folded outbox-prune & broken-spec)

**Status:** PLANNED (backlog only — no code, no commits)
**Created:** 2026-06-22
**Source:** **ADR-0012** (`adr/0012-admin-action-audit-log.md`, **accepted** 2026-06-22) — the frozen
contract + the 6-piece implementation outline; companion living doc
`architecture/decisions/audit-log.md`. Owner approved building the **full** audit-log feature now
(backend + admin UI + tests). Plus two folded-in carry-ins so nothing is dropped.
**Goal:** ship the complete admin action audit log — a pipeline-captured, atomic, append-only
who-did-what trail with handler-emitted before/after on the sensitive few, and its admin read surface —
to ADR-0012's frozen contract, test-first per its test list.

> The audit-driven program (Waves 0–7) is closed/merged. **Wave 8 (pre-iOS cleanup, sprint-10.md) is
> in progress** and stays separate. Wave 9 is the discrete audit-log feature wave. 7 tickets
> (**T-0282…T-0288**), next free ids after T-0281.

---

## 1. Owner decisions this wave builds to

- **Build the full audit-log feature NOW** — backend (entity+migration, behavior, snapshots, query) +
  admin UI + tests. Not a stub.
- **Q-AUDIT-01 (retention/PII) — RESOLVED with the owner's "sensible default now, ratify before prod":**
  - **Retention = keep audit rows INDEFINITELY for now** — no auto-prune of the audit table
    (accountability data is cheap + legally safer to keep; a retention window is a separate pre-prod
    decision).
  - **PII = before/after snapshots store entity ids + the CHANGED fields ONLY, never raw subject PII.**
  - **GDPR-delete is a legal-basis exception to erasure** — the GDPR-delete audit keeps actor + scope +
    subject id (NOT the subject's personal data), so the audit row legitimately **survives** the
    subject's erasure.
  - **Owner ratifies the exact retention window + redaction list before production** (a pre-PROD
    readiness-checklist item, not a blocker for this build).
  - Moved open→answered (`questions/answered.md`); the open-file pre-prod index now points to the
    answer. Baked into **T-0282** (no-delete config), **T-0284** (PII-min + survives-erasure test),
    **T-0287 AC2** (cleanup excludes the audit table).
- **ADR-0010 (durable outbox) is already `accepted` and fully landed** — **no ticket** for it; the only
  outbox follow-on this wave is the non-load-bearing retention-prune (T-0287).

---

## 2. Wave-9 ticket table

| ID | Title | Size | Status | Layers | sec | qa | manual_steps | Lane / batch |
|----|-------|------|--------|--------|-----|----|--------------|--------------|
| **T-0282** | `AdminActionAudit` entity + EF config (TenantId + global filter + 4 indexes) + migration | M | **ready** | db, backend | no | yes | **ef-migration** | **9A FIRST/ALONE** (the spine) |
| **T-0283** | `AuditLogBehavior` (inner-to-UoW, atomic) + `IAuditContext` + `IAuditFailureSink` + `[AuditAction]` + generic capture | M | **ready** | backend | **yes** | yes | — | 9B (after 9A) |
| **T-0284** | Sensitive-five before/after snapshots via `IAuditContext` (typed, pre-redacted, no raw subject PII) | M | **ready** | backend | **yes** | yes | — | 9C (after 9B) |
| **T-0285** | `GetPagedAdminActionAudits` query (canonical `PagedData`) + new `AdminOnly` view policy | M | **ready** | backend | **yes** | yes | **nswag-regen** | 9B (after 9A) · **owns Policy.cs/PolicyBuilder.cs** |
| **T-0286** | Admin `audit-log` feature lib (facade+signals+`cleansia-table`, filters, 5 locales, per-resource history) | M | **ready** (held on regen) | frontend | no | yes | **nswag-regen** | 9D (after 9B + regen) |
| **T-0287** | Outbox retention-prune timer — Dispatched `OutboxMessage` + old `ProcessedMessage` rows (config-driven) | S | **ready** | backend | no | yes | — | **independent** (folded-in) |
| **T-0288** | Fix latent broken `order-management.component.spec.ts` (HttpClient inject — no test provider) | S | **ready** | frontend | no | yes | — | **independent** (folded-in) |

**Reviewer-per-developer on every ticket. Security gate on T-0283 / T-0284 / T-0285** (the compliance/
authz seam). QA on all. **No `L` tickets** — the ADR's 6-piece outline folds into 5 feature tickets
(the test bundle is test-first **inside each** per the ADR's per-piece test list), + 2 folded-in carry-ins.

---

## 3. The 6-piece ADR-0012 outline → 5 feature tickets (the test bundle folds into each, test-first)

| ADR-0012 piece | Ticket | Test-first contract folded in (ADR test list) |
|---|---|---|
| 1. entity + config + migration | **T-0282** | entity-shape / append-only (`init`-only) / tenant-filter + index config tests |
| 2. behavior + `IAuditContext` + failure-sink + generic capture | **T-0283** | **pipeline-order test** + TC-AUDIT-ATOMIC + TC-AUDIT-FAILURE + TC-AUDIT-GATE + TC-AUDIT-LABEL |
| 3. the five sensitive snapshots | **T-0284** | TC-AUDIT-SNAPSHOT (per action + GDPR-survives-erasure, no raw PII) |
| 4. admin paged query + view policy | **T-0285** | TC-AUDIT-QUERY (filters + tenant scope) + authz-rejection integration test |
| 5. admin UI lib | **T-0286** | facade spec test-first (load/filter/empty) + 5-locale + admin prod-build |
| 6. test bundle | — | **not a separate ticket** — each piece lands its slice of the ADR test list test-first (money/state red-first) |

---

## 4. Dependency-ordered batch plan

The audit-log chain is a partly-serial spine (the table → behavior/query → snapshots/UI); the two
folded-in tickets are independent and fan out from day one.

```
T-0282 (entity+migration)  ── FIRST/ALONE on the schema ──┐
        │  (table must exist; owner ef-migration in the bundle, then re-verify)
        ├──────────────► T-0283 (behavior/context/sink, generic capture)  ─► T-0284 (sensitive snapshots)
        └──────────────► T-0285 (query + view policy)  ──(owner nswag-regen)──► T-0286 (admin UI lib)

T-0287 (outbox prune)   ── independent ──► runs concurrently from day 1
T-0288 (broken spec)    ── independent ──► runs concurrently from day 1
```

- **Batch 9A — T-0282 FIRST/ALONE.** Everything in the chain depends on the `AdminActionAudit` table.
  Lands the entity + EF config (TenantId + global filter + 4 indexes) and flags the **ef-migration**
  (bundle B1). **Hold 9B/9C/9D until the owner confirms the migration** — a missing migration trips EF
  `PendingModelChangesWarning` on every integration test.
- **Batch 9B — T-0283 ∥ T-0285** (after 9A + migration confirmed). **Disjoint files:** T-0283 is the
  pipeline/behavior + `IAuditContext`/sink + `[AuditAction]`; T-0285 is the query + the
  Policy.cs/PolicyBuilder.cs cluster. They parallelize (one dev+reviewer each). T-0285 flags the
  **nswag-regen** (bundle B1).
- **Batch 9C — T-0284** (after 9B's T-0283). The five sensitive handlers each emit one `RecordChange`
  into the `IAuditContext` seam T-0283 built. **Serialize against any concurrent edits to the five
  handler files (one writer per file).**
- **Batch 9D — T-0286** (after 9B's T-0285 **and** the owner nswag-regen). The facade/spec may be
  authored test-first immediately, but the ticket is **held from `done`** until the regenerated admin
  client exists + the admin prod-build is clean.
- **Independent — T-0287, T-0288.** No dependency on the audit chain; dispatch concurrently with 9A.

**Dispatch order:** {9A, T-0287, T-0288} day 1 → (owner migration) → {T-0283 ∥ T-0285} → (owner regen)
→ {T-0284 after T-0283; T-0286 after T-0285+regen}.

---

## 5. Lanes / serialization (one writer per shared-file cluster)

- **Policy.cs / PolicyBuilder.cs cluster — T-0285 is the SOLE writer this wave.** The new
  `AdminOnly` **view** policy (e.g. `CanViewAdminActionAudit`) must land in **both** files together or
  `PolicyBuilder.AssertComplete` (`:301-327`) fails boot. No other Wave-9 ticket touches those files; any
  future need serializes behind T-0285.
- **The five sensitive handler files — T-0284 serializes per file.** Refund (`IssuePartialRefund` /
  admin refund), `AdminOverrideOrderStatus`, `EmployeePayConfig` edits, `AdminDeleteUserAccount`/export,
  loyalty grant/revoke, `ResolveDispute`. Never two instances editing the same handler at once.
- **`AuditLogBehavior` ↔ pipeline registration** — T-0283 owns `FluentValidationExtensions.cs:21-23`
  (the outer→inner registration order). T-0285's query rides the same pipeline but adds no behavior.
- **The audit table is OFF-LIMITS to cleanup** — T-0287 (outbox prune) must not touch `AdminActionAudit`
  (ADR-0012 D6 / Q-AUDIT-01 default); its AC2 asserts the exclusion.

---

## 6. Owner manual-steps bundle (B1) — run once, NOT by the agents

Per quality-gates §"batch the owner-only handoffs", one fat handoff, not many thin ones:

1. **ef-migration (T-0282)** — create + apply the migration for the new `AdminActionAudit` table + its
   **4 indexes** (`(TenantId, OccurredOn DESC)`, `(ResourceType, ResourceId)`, `(ActorId, OccurredOn
   DESC)`, `(Action, OccurredOn DESC)`). In PROD apply the new indexes `CONCURRENTLY` by hand if applying
   to a populated table. Current snapshot: `Migrations/20260620160737_Initial.cs`.
2. **nswag-regen — admin client (T-0285)** — regenerate the admin TypeScript client for the new
   `GetPagedAdminActionAudits` / `AdminActionAuditDto` surface; then run **all three** app prod-builds
   (a regen can break an untouched consumer) before pushing. **Unblocks T-0286.**

**Sequencing of the bundle:** the migration is needed **before 9B** (the behavior/query integration tests
run against the real table); the regen is needed **before T-0286** (the UI consumes the generated client).
The PM holds the dependent batches until each is confirmed; the orchestrator re-verifies the merged tree
(`dotnet build` + all three backend suites; the three app prod-builds) once after the owner confirms.

**Pre-PROD readiness-checklist item (NOT a blocker for this wave):** owner/legal ratify the **exact
retention window + redaction list** for `AdminActionAudit` (the Q-AUDIT-01 default ships unchanged until
then).

---

## 7. Folded-in carry-ins (so nothing is dropped)

- **T-0287 — outbox retention-prune** (the outbox verification's non-blocking finding). A config-driven
  `Cleansia.Functions` timer pruning **Dispatched** `OutboxMessage` rows + old `ProcessedMessage` rows —
  table-growth ops hygiene, ADR-0008/0010 flagged it **not load-bearing for correctness**. Size **S**, no
  ef-migration (just a Functions timer + config). **One-line no-decision note** (no new behavior). Excludes
  the audit table. *(ADR-0010 itself was already moved to `accepted` — the durable-outbox code fully
  landed — so there is **no ticket** for that; recorded here only.)*
- **T-0288 — broken `order-management.component.spec.ts`** (Wave-8 leftover, NOT previously
  ticketed-as-open). The spec's `TestBed` provides no `HttpClient` for the standalone component's inject,
  so its `should create` test **fails on `master`** — a latent broken spec. Size **S**, `[frontend]`,
  mechanical harness fix, **no-decision note**.

**Wave-8 boundary (kept clean):** **T-0281** (E2E partner+admin sibling smokes) is **filed and `ready`**
in Wave 8 (sprint-10.md) and **stays in Wave 8's close** — it is NOT pulled into Wave 9. The only new
Wave-8 leftover filed here is the broken spec (T-0288).

---

## 8. Gates & verification (per `agents/process/quality-gates.md`)

- **Reviewer-per-developer** on every ticket (concurrent, not serial).
- **Security gate mandatory on T-0283, T-0284, T-0285** — the compliance/accountability seam: a missed
  admin mutation, a non-atomic success-audit, a dropped failure record, a PII-leaking snapshot, or a
  missing/wrong view policy are security defects. (T-0282 schema-only, T-0286 read-only-consumer,
  T-0287/T-0288 mechanical → no security gate.)
- **QA on all** — AC↔evidence, the ADR test list executed, the ≥-run determinism where applicable.
- **TDD strict on money/state** — the pipeline-order test + TC-AUDIT-ATOMIC/FAILURE/GATE/LABEL/SNAPSHOT/
  QUERY are written **red-first** (the reviewer rejects after-the-fact tests on this material). The
  atomic + out-of-band-sink + survives-erasure behavior is provable **only against real Postgres** — run
  `Cleansia.IntegrationTests` + `Cleansia.HostTests`, not just the unit suite.
- **Mechanical (Gate 8):** `dotnet build` + all three backend suites; the affected app `nx build` +
  `nx affected -t test`; `check-consistency.mjs` no-new-violation (T-0285's query must pass A1/A5).
- **Reviewer compliance checks (ADR-0012 §"How a reviewer verifies"):** #1 behavior placement (inner to
  UoW) · #2 gate (Command + Administrator; queries unaudited) · #3 append-only (no `Modified`/`Deleted`,
  `init`-only) · #4 snapshot ownership (only the five call `RecordChange`; behavior references no domain
  type) · #5 PII minimization (typed snapshots, GDPR scope+ids only) · #6 tenant + config (explicit
  TenantId + global filter + indexes).

---

## 9. Definition of wave-done

Every Wave-9 ticket has an owner, a current state, an `updated` date, satisfied-or-blocked deps, AC with
evidence, and a status-log line per transition. The owner manual-steps bundle B1 is confirmed (migration
applied, admin regen done + all three prod-builds clean). INDEX.md + this doc match reality. Q-AUDIT-01 is
resolved (answered.md) with the owner's default; the exact-window ratification is carried on the pre-PROD
checklist. T-0281 stays in Wave 8's close.

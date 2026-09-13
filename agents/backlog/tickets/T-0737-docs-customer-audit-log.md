---
id: T-0737
title: T-AUD-8 — Docs — ADR-0062 accepted, security rules, model, roles, features, business rules, flows, backlog (ADR-0062 §Consequences)
status: done
size: S
owner: —
created: 2026-09-13
updated: 2026-09-14
depends_on: [T-0730, T-0731, T-0732, T-0733, T-0734, T-0735, T-0736]
blocks: []
stories: []
adrs: [ADR-0062, ADR-0012]
layers: [docs]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

Docs are written at the end, once the feature is green. Seven lanes shipped the customer audit trail
on `fix/remove-membership-free-trial`; ADR-0062 was a challenger-round draft with no verdict; the
security rules still named `CreateCheckoutSession` as un-rate-limited and knew of no customer table;
the payout role card named `AdminMutationGate`, which T-0730 renamed.

## Doing

- `docs/decisions/adr-0062.md` as `accepted` (2026-09-13), rewritten to what shipped: Context /
  D0–D8 / Alternatives / Consequences (with an *"As shipped"* block) / Roles / Verification (every file
  named exists) / the nine rulings recorded. Panel sections dropped. Corrections carried in: the
  request-log suppression names `api/CustomerAudit/timeline` under `/customeraudit/`, not the draft's
  `/api/actiontimeline/`; `AcceptVersion` on a *different* version; `TenantId` NOT NULL; anonymous
  refusals write IP-bearing failure rows bounded by the `auth` window and are skipped before the
  operator exists; enums by name on all three tables; `GetActionTimeline` as a validated paged query
  (A2 deviation) with `Sort` ignored; the `EmployeeActionAudits (OrderId, CreatedOn DESC)` index and
  the final migration id `20260913174821`; the `CreateOrderCommand.termsAccepted` regen gap; the
  unshipped customer link on order/dispute detail.
- `docs/decisions/index.md` (62 records, the ADR-0062 row, the extends-not-supersedes note);
  `docs/.vitepress/config.ts` sidebar; dated notes on ADR-0012 D3 and Q-AUDIT-01.
- `docs/architecture/security-rules.md`: S2's customer paragraph (what a row may and may not hold,
  `Pseudonymise()` the one mutator, the error key, anonymous failure rows, the audited admin export);
  S5's stale `CreateCheckoutSession` sentence replaced by the mechanical guard; S6's suppression table
  names `/customeraudit/`.
- `docs/domain/model.md` (85 entities, 47 stamped / 45 NOT NULL, `CustomerActionAudit`,
  `EmployeeActionAudit`'s index, `UserConsent.DocumentVersion`); `docs/domain/roles/customer-action-audit.md`
  and `audit-gate.md` (new), `index.md`, `employee-payout-details.md` (`AdminMutationGate` → `AuditGate`).
- `docs/product/features.md` (customer export + terms; admin *The customer trail*);
  `docs/product/business-rules.md` §*What is recorded about a customer* (`#customer-record`: the
  sixteen acts, the error key, the terms version constant named, registration/consent recording,
  "3 years per row, default pending Q-AUD-L1", erasure, who reads it).
- `docs/flows/booking-and-pricing.md`, `cancellation-refund-dispute.md`, `auth-and-identity.md`,
  `gdpr-and-audit.md` (retention task, erasure walk, *The customer trail*), `cross-cutting.md`
  (tenancy of an audit row).
- Backlog: T-0730–T-0737 filed `done` with the ticket bodies; T-0686 → `done` (absorbed);
  `questions/open.md` Q-AUD-L1..L6 and Q-AUD-O1..O3 with the default in force and who answers.

## NOT

No code, no tests, nothing under `src/`. Not touched, per the lane's brief (docs/ and agents/backlog
only): `agents/architecture/decisions/audit-log.md` (the "three tables, one pipeline" living-note
section), `agents/knowledge/patterns-backend.md` (the `Audience = Customer` + `RecordEvidence`
canonical shape) and `consistency.md` cross-ref, `agents/cleanup/MANUAL_STEPS.md` MS-2 (still names
`20260913132759`; the owed drop now belongs to `20260913174821`), `CHANGELOG.md`, the stale roster
sentence in `SubjectDataErasureRosterTests` ("today no payload type exists"). `npm run build` — no
shell in the lane; the orchestrator's.

## Acceptance criteria

- [x] **AC1** — `/decisions/adr-0062` is in the index with status `accepted`; the build is the
      orchestrator's to run.
- [x] **AC2** — S2 states what a customer row may and may not hold, that `Pseudonymise()` is the one
      mutator, that the admin subject export is audited; S5 no longer names `CreateCheckoutSession` as
      uncovered.
- [x] **AC3** — `business-rules.md` says "3 years per row, default pending Q-AUD-L1" and names
      `LegalDocumentVersions.CustomerTerms` / `.CustomerPrivacy` = `"2026-09-draft"`.
- [x] **AC4** — `questions/open.md` carries Q-AUD-L1..L6 and Q-AUD-O1..O3, each with the question,
      who answers and the default taken.
- [x] **AC5** — Every file cited in ADR-0062's Verification section exists (checked by glob/grep at
      the tree's HEAD on 2026-09-14).

## Status log

- 2026-09-14 — shipped on `fix/remove-membership-free-trial`. Every sentence verified against the
  tree: `CustomerActionAudit.cs`, `AuditGate.cs`, `AuditErrorCode.cs`, `AuditEntryFactory.cs`,
  `AuditFailureCaptureBehavior.cs`, `OutOfBandAuditFailureSink.cs`, `GetActionTimeline.cs`,
  `CustomerAuditController.cs`, `RequestLoggingMiddleware.cs`, `AdminExportUserData.cs`,
  `GdprExportDto.cs`, the two GDPR controllers, `LegalDocumentVersions.cs`, `UserConsent.cs`,
  `ConsentService.cs`, `GrantConsent.cs`, `Register.cs`/`GoogleAuth.cs`/`AppleAuth.cs`,
  `CreateOrder.cs`, `CancelOrder.cs`, `CreateDispute.cs`, `DataRetentionBackgroundService.cs`,
  `RetentionDefaults.cs`, `GdprDeletionService.cs`, the sixteen markers by grep, the migration
  `20260913174821_Initial.cs` (85 tables; `CustomerActionAudits` with `TenantId nullable: false`; the
  four indexes; `IX_EmployeeActionAudits_OrderId_CreatedOn`; `DocumentVersion varchar(32)`), the admin
  `audit-log` routes and `loyalty-user-detail` template, the generated customer client and the mobile
  spec (the `termsAccepted` gap on `CreateOrder`), the parity checker, and every test file the ADR names.

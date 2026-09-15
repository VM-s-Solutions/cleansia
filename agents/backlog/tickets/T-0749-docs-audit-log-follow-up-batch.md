---
id: T-0749
title: Docs for the audit-log follow-up batch — ADR-0063, ADR-0062/0061 amended in place, living pages, changelog, backlog, knowledge
status: done
size: M
owner: —
created: 2026-09-14
updated: 2026-09-14
depends_on: [T-0738, T-0739, T-0740, T-0741, T-0742, T-0743, T-0744, T-0745, T-0746, T-0747]
blocks: []
stories: []
adrs: [ADR-0063, ADR-0062, ADR-0061]
layers: [docs]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

Docs are written at the end, once the batch is green. Ten tickets shipped the owner's 2026-09-14
rulings on ADR-0062; every living page still described the 2026-09-13 tree (a code constant for the
terms version, "refuse nothing", "no login rows", a JSON incident file, sixteen acts, 62 ADRs, 85
entities, the `20260913174821` migration).

## Doing

- **ADR-0063** (new): entities, immutability, seed-as-source, in-force resolution, placeholders, what
  a new version means, read surfaces, what stays out; consequences; verification.
- **ADR-0062 amended in place**, a dated *"As amended 2026-09-14"* block per decision: D3 (session acts
  recorded, the exact list; the social sign-ins marked as the sign-in with `DeclineSuccessRow`; the
  customer export audited; 25/21/9), D4 (the tick refused on the server incl. the ratified social
  exception; version = effective date → ADR-0063; every client sends the tick; the employee tick;
  the A6 residual), D5 (one commit; the A7 residual and why; dispute text three years; the failed
  erasure record and retry; the export's consent context and what survives erasure), D6 (the PDF and
  what its hash proves; traceability by resource id — how exactly; `GetActionTimeline` canonical;
  admin web), D7 (the session rows' operator), D8 re-cut; Consequences as amended; Verification note;
  the Open questions section rewritten as **§Rulings** with dates and where each landed.
- **ADR-0061** D3 (the `IOperatorScopedRequest` law for anonymous marked commands; five more callers)
  and D4 (the caller table gains `Login`, `MobileLogin`, `ConfirmUserEmail`, `RequestPasswordChange`,
  `ChangePassword`; the adoption before the confirmation check).
- Living pages: `security-rules` S2/S5/S6; `model` (87 entities, `LegalDocument`, `LegalDocumentText`,
  `UserConsent.LegalDocumentId`, `Dispute.TextRetainedUntil`, `GdprRequest` Failed); roles
  (`legal-document`, `incident-file` new; `customer-action-audit` and `audit-gate` updated for the host
  gate and `DeclineSuccessRow`; index); `features`; `business-rules` (the terms gate and keys, the
  retention table incl. `retention.dispute_text.years`, version = effective date, the daily retry,
  the PDF); flows (`auth-and-identity` session rows + the tick rule + the staged revoke;
  `gdpr-and-audit` single-commit erasure, Failed rows + retry, the dispute window, the incident
  file; `booking-and-pricing` the tick rule; `cross-cutting` the session rows' tenant and the Failed
  row); `local-orchestration#legal-seed`; `infrastructure` timers (22, `RetryFailedUserDeletions`,
  `DisputeText`); the admin and customer app overviews; decisions index (63) + sidebar.
- `CHANGELOG.md` entries in customer / cleaner / admin / operator / API-consumer voice.
- `agents/backlog`: T-0738 … T-0749 rows and ticket files (T-0747 corrected), T-0748 open, Q-AUD-L1 …
  O3 deleted into ADR-0062 §Rulings, Q-GDPR-01 / Q-GDPR-02 / Q-AUD-O4 filed.
- `agents/knowledge`: `consistency.md` A2 (no declared exception), `patterns-frontend.md` (the
  blankable-copy idiom has no user), `patterns-backend.md` (a paged query's validator runs);
  `agents/architecture/decisions/audit-log.md` living note; `agents/cleanup/MANUAL_STEPS.md` MS-2 →
  `20260914115922`; `CLAUDE.md` counts (63 ADRs, 27 roles).

## NOT

No code, no tests, nothing under `src/`. `npm run build` — no shell in the lane; the orchestrator's.

## Acceptance criteria

- [x] **AC1** — `/decisions/adr-0063` exists with status `accepted` and is in the index and sidebar.
- [x] **AC2** — ADR-0062 carries a dated *As amended* block on D3, D4, D5, D6, D7, D8 and §Rulings
      names all nine questions with the ruling and where it landed.
- [x] **AC3** — `business-rules#customer-record` states the terms-refusal rule and its keys, the
      retention windows incl. `retention.dispute_text.years`, version = effective date and the daily
      retry.
- [x] **AC4** — `questions/open.md` carries no Q-AUD-L*/O1..O3 heading and carries Q-GDPR-01,
      Q-GDPR-02, Q-AUD-O4.
- [x] **AC5** — Every file named in ADR-0063's Verification and in ADR-0062's amended Verification
      note exists at the tree's HEAD (checked by glob on 2026-09-14).

## Status log

- 2026-09-14 — shipped on `fix/remove-membership-free-trial`. Every sentence verified against the
  tree: `LegalDocument.cs`, `LegalDocumentText.cs`, `LegalSeedResource.cs`, `LegalDocumentSeeder.cs`,
  `LegalDocumentSeedHostedService.cs`, `LegalDocumentRepository.cs`, `LegalDocumentResolver.cs`,
  `LegalMarkdownRenderer.cs`, `GetLegalDocument.cs`, the two `LegalController`s and
  `AdminLegalController.cs`, `ConsentService.cs`, `GrantConsent.cs`, `Register.cs`, `CreateOrder.cs`
  (validator + handler), `AuditGate.cs`, `IAuditContext.cs`, `LoginEvidence.cs`, `ConfirmUserEmail.cs`,
  the 25 markers by grep, `GdprRequest.cs`, `RetentionDefaults.cs`, `DataRetentionBackgroundService.cs`,
  `GdprDeletionService.cs`, `RetryFailedUserDeletions.cs`, `AdminRetryUserDeletion.cs`,
  `ErasureFailureCaptureBehavior.cs`, `ExportCustomerIncidentFile.cs`, `IncidentFileService.cs`,
  `IncidentFileDigest.cs`, `GdprExportDto.cs`, `AdminGdprController.cs`, `DbContextBindingExtensions.cs`,
  `BusinessErrorMessage.cs`, the fourteen `IOperatorScopedRequest` records, the migration
  `20260914115922_Initial.cs` (87 tables, the legal tables and indexes, `IX_Disputes_TextRetainedUntil`,
  `FK_UserConsents_LegalDocuments_LegalDocumentId` Restrict), the customer locale `legal.*` keys, the
  admin `legal-documents` lib and route, the Functions timer files (22 timers — `ExpireStaleCredit`
  was missing from the inventory), and every test file the two ADRs name. **The orchestrator's brief
  named the migration id as `20260914081247`; the tree holds `20260914115922`** — the docs follow the
  tree.

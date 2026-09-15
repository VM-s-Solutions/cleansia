---
id: T-0752
title: The subject export carries disputes, the self-export's request row names `self`, and the incident file prints the operating company and market (Q-GDPR-02 + two S4/S6 minors)
status: done
size: S
owner: —
created: 2026-09-15
updated: 2026-09-15
depends_on: [T-0743, T-0746, T-0751]
blocks: []
stories: []
adrs: [ADR-0062]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

Owner ruling 2026-09-15 on **Q-GDPR-02** (filed by T-0743): *yes — the export carries the disputes*.
Two security minors found on the way and fixed in the same ticket: the customer's own `ExportUserData`
stamped the subject's live e-mail into `GdprRequest.ProcessedBy` (a row that outlives the erasure —
S6), and the PDF incident file printed the raw `TenantId` under *Operator* (an internal identifier on
a document built to leave the platform — S4).

## Doing

- **Disputes in the export**, both callers (one `GdprExportService`): `GdprExportDisputeDto` per
  dispute that is the subject's — filed on the account, or on an order of `SubjectOrders` (the set the
  orders section lists; the order term alone is read past the tenant filter) — with the dispute id,
  the order id and display number, the reason and status as enum **names** (the document is read by
  the subject; the wire converter writes the other sections' enums as integers), the description, the
  resolution notes, the refund amount with the order's currency code, created/resolved stamps, every
  message as (author role, time, text) — the staff member's id never leaves — and the evidence file
  names. Text as stored: description, messages and notes until the three-year window closes and the
  marker after the sweep; evidence names as the marker from the erasure on. `GdprExportEvidence` and
  `GdprExportSnapshot` carry `DisputeCount`.
- **The self-export's request row** stamps `GdprAuditReasons.SelfActor` (`"self"`), the idiom the
  deletion fix introduced; the `SelfActor` doc names both callers.
- **The incident file's identity section**: `IncidentFileSubject.Operator` → `OperatorName` +
  `Market`, resolved by `IncidentFileService` from the market registry — the `CountryConfiguration`
  rows whose `OperatorTenantId` is the user's, with the `Tenant`'s display name and the served
  countries as "Name (ISO2)", joined and ordered; both the em-dash marker when no configuration names
  the operator. The tenant id appears nowhere in the section model the layout and the SHA-256 walk.

## NOT

No PDF for the customer. No change to the export's trail section (T-0751's decision). No change to
the incident file's order set. No schema change. No hand-edit of the generated clients.

## Status log

- 2026-09-15 — shipped in `9c0b9801` (red first with the fixes flipped back: Tests 2 failed / 21
  passed in the `ExportUserDataAuditEvidenceTests|IncidentFileServiceTests|IncidentFileDocumentTests`
  filter, IntegrationTests 4 failed / 8 passed in `SubjectExportDisputesTests|IncidentFileTests|SubjectExportAuditTests`;
  green after: Tests 5456/5456, IntegrationTests `Features.Gdpr` 38/38 on Postgres, HostTests
  239/239) and `31fac812` (review: the dispute read's tenant bypass names its own residual — a dispute
  on a guest booking stamped with another market's operator, which no writer produces today;
  `GdprExportDisputeDto`'s doc bounds the order term; `IncidentFileSubject`'s doc names the em-dash
  marker; no behaviour change).
- **Owed after the phase (the review's major finding, by design not addressed in the backend):** the
  generated NSwag `GdprExportDto` class's `toJSON()` enumerates only the members it was generated
  with, so both web facades' `JSON.stringify` drop the `disputes` section from the downloaded file
  until the customer, admin and partner clients are regenerated — the orchestrator's regen step after
  this phase (needs the three hosts up). The API response carries the section from `9c0b9801` on.
  The row is `done` on the orchestrator's instruction with this dependency named; the mobile specs owe
  a re-dump for `GdprExportEvidence.disputeCount` / `GdprExportDto.disputes` too.
- The account-term reading of "the subject's disputes" (review finding 2) is left as built pending
  the owner's one-sentence ratification. Recorded in ADR-0062 D6 as amended 2026-09-15;
  `/flows/gdpr-and-audit#subject-export`; the incident-file card; S2/S4.

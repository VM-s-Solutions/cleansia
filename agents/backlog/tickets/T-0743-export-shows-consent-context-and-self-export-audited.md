---
id: T-0743
title: The export shows what was consented and the customer's own export is audited (A5, O3)
status: done
size: S
owner: —
created: 2026-09-14
updated: 2026-09-14
depends_on: [T-0742]
blocks: []
stories: []
adrs: [ADR-0062, ADR-0063]
layers: [backend, frontend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

Owner rulings 2026-09-14. **A5** "both" — `GdprExportConsentDto` carried no `IpAddress` /
`UserAgent` / `DocumentVersion` although `UserConsent` has all three. **O3** *"I want it audited too"*
— the customer's own `ExportUserData` was a Command with no audit marker.

## Doing

- `GdprExportConsentDto` gains `IpAddress`, `UserAgent`, `DocumentVersion` and `LegalDocumentId`.
- `ExportUserData.Command` gets `[AuditAction("customer.gdpr.export", Audience = Customer, ResourceType
  = "User")]` with a counts-only evidence record; a throwing build → one out-of-band customer failure
  row. Roster test, PII guard, five admin locale labels (`customer-audit-actions.ts`).
- Tests: the export sections on Postgres; the audit row on the self-export; roster.

## NOT

No PDF (T-0746). No change to the admin export.

## Status log

- 2026-09-14 — shipped in `4ecfd9bd` (the four consent fields for both callers;
  `GdprExportEvidence(OrderCount, ConsentCount, CustomerActionCount)` — three counts, not the ticket's
  four; roster 17 commands / 16 labels at that point; the S6 path-suppression test pins the consent
  user agent as raw-but-suppressed) and `1a4fd471` (the DTO summary's claim that the request context is
  null after an erasure was false — the erasure only withdraws the consent, so IP, user agent, version
  and document id **survive** on the withdrawn row; the erased-subject export test now asserts it).
  Reported, not built: the JSON export carries no dispute section (Q-GDPR-02). Recorded in ADR-0062
  D3/D5 as amended.
- 2026-09-15 — Q-GDPR-02 ruled *yes* and shipped as **T-0752** (`9c0b9801`): the export carries the
  disputes; `GdprExportEvidence` gained `DisputeCount`.

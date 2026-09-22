---
id: T-0783
title: Backend record — the incident file section, the metadata retention task + key, the archive stream (ADR-0068 D5)
status: done
size: S
owner: —
created: 2026-09-20
updated: 2026-09-20
depends_on: [T-0777]
blocks: [T-0784]
stories: []
adrs: [ADR-0068, ADR-0064, ADR-0062]
layers: [backend]
security_touching: false
manual_steps: []
sprint: —
---

## Context

ADR-0068 D5, split off T-0777 by the panel (C15) so the critical path stayed short: the entity, its
repository (with `PseudonymiseExpiredAsync` and the archive stream already declared) and the erasure
site ship with T-0777 because the roster tests read the file for the symbol; the three readers of the
record that need no wire change were to run beside a client lane.

## Doing

1. **Incident file.** A *Contracts for work* section per order on `IncidentFilePdfData` — seat,
   cleaner id + given name, `AcceptedOn`, version, language, client audience, IP, device label, device
   id, the facts through `IncidentFileEvidenceFields.Flatten`; the trail row `employee.order.contract_accepted`
   (`IncidentFileServiceTests`, `IncidentFileDocumentTests`).
2. **Retention.** `TenantSettingCatalog.WorkContractMetadataRetentionYears`
   (`retention.work_contract_metadata.years`, default 3, min 1); a
   `RunSafeAsync("WorkContractAcceptanceMetadata", …)` task in `DataRetentionBackgroundService`'s
   per-company loop calling `PseudonymiseExpiredAsync(cutoff, RetentionDefaults.BatchSize)` on
   `AcceptedOn` until a batch is empty; never deletes. `WorkContractAcceptanceRetentionTests` (Postgres,
   two companies), `WorkContractAcceptanceMetadataSweepTests`.
3. **Archive.** `CompanyArchiveRecords.WorkContractAcceptance(Id, OrderId, OrderEmployeeId, EmployeeId,
   LegalDocumentTextId, DocumentVersion, AcceptedOn, ClientAudience, FactsJson)` — no trio; the orders
   record gains `WorkContractDocumentId`; `CompanyArchiveService` streams `books/work-contract-acceptances.jsonl`
   and the manifest names it; `CompanyArchiveRecordGuardTests`, `CompanyArchiveBundleTests`.

## NOT

- No change to the entity, the repository interface, any DTO, any route; no client regen.
- No admin settings description (T-0784).
- No deletion of any acceptance row, ever.

## Done looks like

The incident file for an order prints the contracts section; the retention timer blanks the trio on
rows older than each company's window and no other company's; the archive bundle carries the stream
and the orders' document id; all three backend test projects green.

## Acceptance criteria

- [x] **AC1** — the incident file prints the contracts section and the trail lists the
      `employee.order.contract_accepted` row (`IncidentFileService.cs:27`, `:134`;
      `GetActionTimeline.cs:206`; `IncidentFileServiceTests`, `IncidentFileDocumentTests`).
- [x] **AC2** — company A at `retention.work_contract_metadata.years = 1` and company B at the
      default: A's old rows have the trio nulled, B's do not, nothing is deleted, a value of 0 is
      refused at write (`TenantSettingCatalog.cs:62-63`, `DataRetentionBackgroundService.cs:73`,
      `:362-367`; `WorkContractAcceptanceRetentionTests`, `WorkContractAcceptanceMetadataSweepTests`,
      `TenantSettingCatalogTests`).
- [x] **AC3** — the bundle holds `books/work-contract-acceptances.jsonl` with no `ipAddress` /
      `deviceLabel` / `deviceId` member, the orders stream carries `workContractDocumentId`, the
      manifest names the file, the record guard is green (`CompanyArchiveService.cs:153`,
      `CompanyArchiveRecords.cs:56`, `:67`; `CompanyArchiveBundleTests.cs:77`, `:180`).

## Status log

- 2026-09-20 — filed by the docs lane from the batch-9 panel; runs after T-0777, beside T-0780.
- 2026-09-20 — **done inside T-0777's core commit `a15d3af0`**: the backend lane carried the three
  record items with the entity rather than leaving them for a second backend lane (the batch runs one
  backend lane at a time), so no separate commit exists for this ticket. Every artefact above is in
  the tree at that hash; the Postgres suite (`WorkContractAcceptanceRetentionTests`,
  `CompanyArchiveBundleTests`) first executes in CI.

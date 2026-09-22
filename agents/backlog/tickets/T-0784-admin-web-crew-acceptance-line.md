---
id: T-0784
title: Admin web — the crew acceptance line with Read on the order detail; the settings description key (ADR-0068 D4, D5)
status: done
size: S
owner: —
created: 2026-09-20
updated: 2026-09-20
depends_on: [T-0777, T-0783]
blocks: []
stories: []
adrs: [ADR-0068, ADR-0066]
layers: [frontend]
security_touching: false
manual_steps: []
sprint: —
---

## Context

ADR-0068 D4 (challenge C9): the only path to a pending seat is the admin's own act
(`AdminReassignOrder` writes no acceptance), so the admin must see it on the order detail rather than
learn it from a phone call. D5: the tenth retention window needs its description in the admin app's
five locales or the Company settings page renders the raw key. Runs after T-0777's regenerated
`admin-client.ts` (the type label already landed there) and T-0783's catalogue key, beside T-0781.

## Doing

- The admin order detail's crew list: per entry *accepted {date}, v{version}* when a
  `workContractAcceptances` entry has that seat's `id` as `orderEmployeeId`, else *contract pending*;
  **Read** on an accepted one opens `components/admin-work-contract-dialog` —
  `getWorkContract(acceptanceId, uiLanguage)`, the facts and the acceptance rows, `[innerHTML]`
  through the sanitizer. Five locales; a spec pinning both states and the read.
- `descriptions.retention.work_contract_metadata.years` in the admin app's five locales beside
  `customer_audit`.

## NOT

- No admin acceptance on a cleaner's behalf; no change to `AdminReassignOrder`'s screen beyond the
  line; no PDF; no legal-documents page work beyond what T-0777 landed.

## Done looks like

An admin opening an order sees, per crew member, whether the contract is accepted and can read an
accepted one; the Company settings page shows the new window with a sentence; Jest green.

## Acceptance criteria

- [x] **AC1** — one accepted and one pending seat: *accepted {date}, v{version}* on the first and
      *contract pending* on the second; **Read** renders the accepted text and facts
      (`order-detail.models.spec.ts`, `order-detail.component.spec.ts`,
      `admin-work-contract-dialog.component.spec.ts`, `admin-work-contract-dialog.facade.spec.ts`).
- [x] **AC2** — `retention.work_contract_metadata.years` renders on the Company settings page with its
      description in five locales (`tenant-setting-catalogue.spec.ts` ties the locales to the backend
      catalogue).

## Status log

- 2026-09-20 — filed by the docs lane from the batch-9 panel; runs after T-0777 and T-0783, beside T-0781.
- 2026-09-20 — done in **`1dd91c24`** (*the order detail says per crew member whether the contract for
  work is accepted, Read opens the accepted text with the frozen facts and the accepted row's SHA-256,
  and the retention window has its sentence*) + **`0d663d1b`** (*the contract dialog reads the
  accepted text's hash only for a session that may, says so for the other roles, and takes the fact
  rows from one shared helper*). Beyond the ticket's ask: the dialog reads `adminLegalClient.getDocument`
  for the accepted language and prints that text row's SHA-256 — the hash a dispute cites (ADR-0068 D4's
  bundle procedure) — only for a session holding `CanViewLegalDocuments` (Administrator); the other
  roles see a line saying the hash is the Administrator's to read.

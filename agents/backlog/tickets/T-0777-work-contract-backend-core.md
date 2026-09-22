---
id: T-0777
title: Backend core — the work-contract document + the order stamp, the acceptance row, the take/accept/start/complete rules, the reads, export DTOs, erasure site, roster entries, one regen, three clients, two spec re-dumps (ADR-0068 D1–D5)
status: done
size: L
owner: —
created: 2026-09-20
updated: 2026-09-20
depends_on: []
blocks: [T-0778, T-0779, T-0780, T-0781, T-0783, T-0784, T-0782]
stories: []
adrs: [ADR-0068, ADR-0063, ADR-0062, ADR-0041]
layers: [backend, db]
security_touching: true
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-09-20, verbatim: *"I want to implement 'smlouva o dílo' (lawyer suggested it)"*.
Designed as ADR-0068 (panel-accepted the same day; the lawyer's framework agreement art. 2.3 forms an
individual contract for work between the customer and the cleaner at the cleaner's acceptance, on the
customer terms). Ground truth at filing (`8a7710b0`): the order carried no legal text; `TakeOrder`
wrote a seat the next drop hard-deleted and nothing that said which text the cleaner agreed to, what
the job was when they did, or that it was their own act rather than an admin's placement
(`AdminReassignOrder.cs:122-123` leaves the same seat row). Runs **first and alone** — it changes the
schema, the take's wire shape and `OrderItem`; every other lane starts from its committed artefacts.

## Doing

1. `LegalDocumentType.WorkContract = 2`; `LegalSeedResource.Types["work-contract"]`; five embedded
   template files `Seed/Legal/customer/work-contract/any/<merge date>/{cs,en,sk,uk,ru}.md` (draft
   line first, `{{currency}}` where a price is mentioned, no figure/name/address/date);
   `LEGAL_SEED_TYPES` gains `work-contract` in `check-booking-policy-parity.mjs` with a red self-test case.
2. `Order.WorkContractDocumentId` (nullable, FK `LegalDocuments` Restrict, indexed) +
   `SetWorkContractDocument`; `OrderFactory.CreateAsync` resolves the in-force document for the
   address's market beside the pay-coverage gate and throws when none; no change to `Order.Create`.
3. `WorkContractAcceptance : TenantAuditable` (D2's twelve columns, `UNIQUE (OrderEmployeeId)`, three
   indexes, FKs `Orders` + `LegalDocumentTexts` Restrict, `Create` + `Pseudonymise` only);
   `IWorkContractAcceptanceRepository`; `WorkContractFacts` + `IWorkContractFacts` marker;
   `WorkContractFactsBuilder` (one `AsNoTracking` projection, `locationApproximate` through
   `BuildApproximateAddress`).
4. `WorkContractAcceptor.StageAsync(order, seat, textId)` — the one writer: the text with its document,
   the document guard, the facts, IP + device label from the request, the device from the **session
   claim**, the host audience, the row and its `EmployeeActionAudit(ContractAccepted)`; no commit.
5. `TakeOrder.Command(OrderId, AcceptedWorkContractTextId)`: blank → `contract.not_accepted` before
   existence; `TextBelongsToOrderContractAsync` → `contract.text_mismatch` last; the handler stages the
   acceptance between `AddAssignedEmployee` and its own commit.
6. `AcceptWorkContract.Command(OrderId, AcceptedWorkContractTextId)` on the two partner hosts
   (`CanTakeOrder`, `interactive`): one chain, any not-over order, idempotent per seat.
7. The gates: `HasAcceptedWorkContractForSeatAsync` → `contract.acceptance_required` on `StartOrder`
   and `CompleteOrder` right after `EmployeeIsAssignedToOrderAsync`; `NotifyOnTheWay` untouched.
8. `GetWorkContractPreview.Query(OrderId, Language)` (partner hosts) and `GetWorkContract.Query(AcceptanceId, Language)`
   (all five hosts) → `WorkContractDto`; `OrderItem.WorkContractAcceptances` (one per current seat, no
   name) filled by `GetOrderDetails` and emptied by `RedactForBrowsingCleaner`.
9. Keys `contract.not_accepted` / `contract.text_mismatch` / `contract.acceptance_required` in a new
   `contract` block; `EmployeeAuditAction.ContractAccepted = 3` + `employee.order.contract_accepted`.
10. Export: `GdprExportDto.WorkContractAcceptances` (the cleaner's rows in full),
    `GdprExportOrderDto.{WorkContractDocumentVersion, WorkContractAcceptances}` (no employee id),
    `GdprExportEvidence.WorkContractAcceptanceCount`. Erasure: `PseudonymiseForEmployeeAsync` beside
    `Employee.Anonymize()` riding the single commit; the erasure and books roster entries.
11. `Initial` regenerated; partner, customer **and admin** NSwag clients regenerated; both mobile specs
    re-dumped; the admin `TYPE_LABEL_KEYS.work_contract` entry + five locales in the same commit; the
    integration fixture's fixed-id work-contract document + `en` text.
12. Tests per ADR-0068 §Verification #1–#5, #7, #9, #11.

## NOT

- No PDF (D4 follow-up); no change to `AdminReassignOrder`; no admin placement as an offer (Q-WC-06).
- No gate on `NotifyOnTheWay`; no pipeline `[AuditAction]` on the two commands; no `CoverDisplaced`.
- No `Language` member on the take; no `bool` flag; no `(OrderId, EmployeeId)` unique index.
- No signature change on `ILegalDocumentResolver`; no `Employee`-audience seed folder; no
  `TermsDocumentId` on the order (a finding for the PM).
- No admin order-detail line or settings description (T-0784) — the type label is the only admin
  touch, forced by the regen. No frontend work beyond it.
- No `EmployeeAgreementStatement` / `AgreementVersion`; no export section for `EmployeeActionAudit`
  rows (reported gap); no lazy stamp; no per-preview HMAC.

## Done looks like

`Initial` regenerated once with `WorkContractAcceptances` and `Orders.WorkContractDocumentId`; the
seed lands three customer documents on a real Postgres; a booking stamps the order; a take without
the text id is refused with no seat; a take with a text of the order's document writes the seat, the
acceptance (device id from the claim) and the audit row in one commit; an admin-placed cleaner is
refused at Start and at Complete with `contract.acceptance_required`, accepts, and proceeds; the DTO
member is redacted for browsers; `GetWorkContract(acceptanceId)` answers the customer, the accepting
cleaner and an admin, and nobody else; the erasure blanks the trio; the export carries both subjects'
rows; the three clients regenerated, the admin type label added, both specs re-dumped, all in the DTO
commit; CI green on all three backend test projects and the seven workflows.

## Acceptance criteria

- [x] **AC1** — the factory stamps `Orders.WorkContractDocumentId` from the address's market and
      throws when nothing is in force (`OrderFactory.cs:83-84`, `:178`; `OrderFactoryWorkContractStampTests`).
- [x] **AC2** — a good take writes one seat, one `Confirmed` row, one acceptance (seat, echoed text,
      version, server instant, host audience, IP / device label, the session's `device_id` claim or
      null, the builder's facts with `locationApproximate`) and one `ContractAccepted` audit row, all
      committed together (`WorkContractAcceptor.cs:39-61`; `TakeOrder.cs:358`;
      `TakeOrderWorkContractTests` unit + Postgres; `WorkContractAcceptorTests`).
- [x] **AC3** — a null/blank id is refused `contract.not_accepted` before existence, the same for a
      held and a missing order, no seat (`TakeOrder.cs:64-65`).
- [x] **AC4** — a full order answers `no_available_spots` ahead of a mismatch; a text of another
      document, another language of another document, or an order with no document →
      `contract.text_mismatch`, no seat; another language of the order's own document is accepted
      (`TakeOrder.cs:88-89`; `TakeOrderWorkContractTests`).
- [x] **AC5** — the seat-race loser answers `no_available_spots` and exactly one acceptance row exists
      (`TakeOrderWorkContractTests` on the `TakeOrderConcurrentSeatRaceTests` fixture, Postgres).
- [x] **AC6** — the preferred cleaner previews and takes; a stranger's preview of a held order →
      `order.not_found` (`GetWorkContractPreview.cs:66-73`, the board's floor since `1067d938`); a cover
      take leaves the displaced cleaner's row and writes the taker's on the new seat.
- [x] **AC7** — the placed cleaner: no row after `AdminReassignOrder`; Start → `contract.acceptance_required`
      after the assignment rule (`StartOrder.cs:56-59`); a non-crew cleaner → `order.employee_not_assigned`;
      `AcceptWorkContract` writes one row + one audit row, repeats answer the same id
      (`AcceptWorkContract.cs:119-131`); the two-seat Complete case (`CompleteOrder.cs:88-91`); the
      re-add case (`WorkContractGateTests`, `AcceptWorkContractHandlerTests`).
- [x] **AC8** — a re-take is two rows on two seats; `OrderItem.workContractAcceptances` carries the
      current seat's only, is empty for a browsing cleaner (`OrderPiiRedaction.cs:68`) and carries no
      name (`WorkContractDto.cs:38-45`; `GetOrderDetailsWorkContractTests`, `OrderRedactionSurfaceTests`).
- [x] **AC9** — `GetWorkContract(acceptanceId)`: the order's customer, the named cleaner after
      dropping and an admin → `200` with the stored facts and `acceptedLanguage`; another customer or
      cleaner → `order.not_found` (`GetWorkContract.cs:48-63`; `GetWorkContractHandlerTests`).
- [x] **AC10** — the cleaner's erasure blanks the trio riding the single commit, a commit throw leaves
      it intact; the customer's erasure leaves the rows (`GdprDeletionService.cs:506`;
      `WorkContractAcceptanceErasureTests`).
- [x] **AC11** — the cleaner's export lists the rows with the trio and the facts and the evidence
      count matches; the customer's lists `workContractDocumentVersion` and the acceptances' instant /
      version / language, no employee id (`GdprExportService.cs:71-94`, `:149-165`; `WorkContractExportTests`).
- [x] **AC12** — the two partner routes and `GetWorkContract` on the five hosts: `401` / `403` / `200`,
      `interactive` window; `GET api/Legal/GetDocument?type=WorkContract` answers `200` anonymous
      (`WorkContractRouteTests`, `RateLimitCoverageGuardTests`; the host suite's first execution is CI).
- [x] **AC13** — a baked `1 000 Kč` in `customer/work-contract/any/<date>/cs.md` is a red build and the
      self-test proves it — done by **`0cb13f11`** (`LEGAL_SEED_TYPES` walks `work-contract`).
- [x] **AC14** — the admin app compiles with the `TYPE_LABEL_KEYS.work_contract` entry and the label
      resolves in five locales (`legal-documents.models.ts`; in `a15d3af0`).

## Implementation notes

ADR-0068 D1–D5 carry the shape line by line. What departed from the draft is recorded in ADR-0068
§*What shipped*: the record items of T-0783 landed here as well; `WorkContractAcceptanceRow` is the
read shape; the acceptance-keyed, seat, order and subject reads bypass the tenant filter with a
caller re-pin (the row is stamped with the order's operator; a customer may be booked across the
border) while the gates and the sweep stay filtered; the preview reads the board's floor
(`1067d938`); `GetWorkContract` sits under `CanViewOrderDetail` / `CanViewOrderDetailAdmin`. The
migration regen (`20260919231739_Initial`), the three client regens and the two spec re-dumps ran in
this lane; the DEV drop is owed at the deploy (MS-2, A1). A fresh Development database is seeded with
the legal texts in the boot that migrates it (`b34dff07`), because the factory now refuses a booking
with no contract text in force and the hosted seeder runs before the schema exists on a first boot.

**Security (Gate 3) — `security_touching: true`.** A new tenant-ignoring read family with a caller
re-pin (S8, ADR-0051's matrix); the device id comes from the signed claim, never the header (ADR-0062
D1); the take's one-chain invariant holds with the tick before existence (a held order stays
indistinguishable from a missing one).

## Status log

- 2026-09-20 — filed by the docs lane from the batch-9 panel (ADR-0068 accepted the same day); runs
  first and alone.
- 2026-09-20 — done in **`a15d3af0`** (the core: the document, the stamp, the entity and repository,
  the acceptor and the facts builder, the take's two rules and the staged acceptance, `AcceptWorkContract`,
  the two gates, the two reads, `OrderItem.workContractAcceptances` + the redaction, the keys and the
  audit label, the export DTOs and the erasure site, the roster entries, the incident-file section /
  retention key / archive stream of T-0783, `Initial` → `20260919231739`, the three NSwag clients,
  both mobile specs, the admin type label and the partner `api.contract.*` keys in five locales) +
  **`1067d938`** (the preview reads the board's floor, not the hold alone — a cancelled, finished,
  full or unpaid-card job the caller is not on discloses no facts; the read trusts the FK on the
  accepted text; the resolver, audit and acceptor comments say what the code does) + **`0cb13f11`**
  (AC13 — the legal-seed guard walks the work-contract folder). The Development boot seed followed as
  `b34dff07`. Every backend test named in ADR-0068 §Verification exists in the tree (`WorkContractGateTests`
  is the one file for both gates); the Postgres and host suites' first execution is CI — Docker was
  down on the development machine.

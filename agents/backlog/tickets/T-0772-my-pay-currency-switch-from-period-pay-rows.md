---
id: T-0772
title: My Pay — the currency switch comes from the period's pay rows (F6)
status: todo
size: S
owner: —
created: 2026-09-19
updated: 2026-09-19
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, frontend]
security_touching: true
manual_steps: []
sprint: —
---

## Context

Owner-plate finding F6 (2026-09-16): the partner web My Pay currency switch appears only once a period has
invoices and offers a cancelled invoice's currency too — it mirrors `GetPeriodPays`, which reads every
invoice regardless of status and falls back to the resolved currency when uninvoiced. **Owner ruling
2026-09-19:** the switch should exist *"for any period holding pays in more than one currency"*.
Orchestrator default applied: `GetPeriodPays`' response gains the distinct currencies of the period's
**pay rows** (an additive DTO member); the web derives the switch from it; mobile untouched. No ADR; the
design was argued by the batch-6 panel (challenge E1–E2) and is recorded here.

**Ground truth at filing** (re-checked 2026-09-19). The server answers one currency
(`GetPeriodPays.cs:82-101`): the view is the named currency, else the cleaner's resolved currency with the
invoiced-elsewhere fallback (`:90-96`); it loads **every** pay row of the (employee, period) with `Currency`
included (`OrderEmployeePayRepository.cs:68-77`) and then filters to the view (`:98-101`).
`PeriodPaySummaryDto.CurrencyCode` names the view (`PeriodPaySummaryDto.cs:18-29`). The web derives the
switch from invoices (`period-pay.facade.ts:183-200`: a `getPagedInvoices` call on every period change,
mapped by `getPeriodCurrencies(invoices)`, `period-pay.models.ts:11-24`), shows it when more than one
(`hasMultipleCurrencies`, `:41`), and reloads the summary with the chosen `currencyId`. So an **open**
(uninvoiced) period with pay rows in CZK and EUR shows no switch and the EUR rows are unreachable; a
**cancelled** invoice's currency is offered even when no live pay row is in it.

## Doing (backend)

- `PeriodCurrencyDto(Id, Code)`; `PeriodPaySummaryDto.AvailableCurrencies` (nullable, defaulted) = the view
  currency first, then every other distinct pay-row currency of the employee and period, ordered by code —
  computed from the already-loaded rows **before** the view filter at `GetPeriodPays.cs:98-101` (zero extra
  queries).
- Unit tests `GetPeriodPaysHandlerTests`: CZK + EUR rows, no invoice, view CZK → `[CZK, EUR]` and CZK rows
  only; CZK only → `[CZK]`; no rows, no invoice → `[resolved]`; named `CurrencyId = EUR` with CZK rows only →
  `[EUR, CZK]`; a cancelled USD invoice with no USD pay row → USD not offered; distinct and ordered by code
  after the first entry. The partner NSwag client regenerated and committed.

## Doing (web)

- `period-pay.facade.ts`: drop the `getPagedInvoices` branch and the `forkJoin`; `periodCurrencies` from
  `summary.availableCurrencies ?? []`; `getPeriodCurrencies(summary)` replaces the invoice mapper;
  `hasMultipleCurrencies` unchanged (`length > 1`); `selectedCurrencyId` = the entry whose code equals the
  summary's `currencyCode` (always present — the first); `selectPeriod` still resets the currency to null;
  `PERIOD_INVOICES_LIMIT` and the invoices import go; specs.

## NOT

- Mobile untouched. **The mobile spec re-dump is skipped deliberately**: the member is additive and
  nullable, both apps ignore unknown keys (`NetworkModule.kt:82`; Swift `Codable` default), and the
  committed specs are the mobile lanes' baseline — the re-dump belongs to the first mobile ticket that reads
  the member. The report says so. No admin period-pay change. No invoice changes.

## Done looks like

An open period with pay rows in two currencies shows the switch; a period whose only second currency is a
cancelled invoice's does not; the selected option always equals the summary's currency; the partner web
makes one call per period load.

## Acceptance criteria

- [ ] **AC1** — *Given* an uninvoiced period with CZK and EUR pay rows and a CZK-resolved cleaner, *when* My
      Pay loads, *then* the switch shows CZK (selected) and EUR; *when* EUR is chosen, *then* the EUR rows
      load.
- [ ] **AC2** — *Given* CZK rows only, *then* no switch.
- [ ] **AC3** — *Given* a cancelled USD invoice and no USD pay row, *then* USD is not offered.
- [ ] **AC4** — *Given* `CurrencyId = EUR` named with CZK rows only (a deep link), *then*
      `AvailableCurrencies = [EUR, CZK]` and `OrderPays` is empty (handler test).
- [ ] **AC5** — The facade never calls `getPagedInvoices`; `PERIOD_INVOICES_LIMIT` is gone.

## Implementation notes

*Why the view currency is always first:* the switch must always contain the value it shows — a period
viewed from an invoice in a currency that (after a cancellation) has no live pay row would otherwise render
a select with no selected option; with the view first, `AvailableCurrencies.Count > 1` is exactly "there is
something else to switch to". *Why pay rows and not invoices:* a pay row is the fact; an invoice is a
document over rows, absent on an open period and present-but-cancelled after a cancel; the ruling names
pays. *Why a two-field DTO:* the label needs the code and the facade has no currency catalogue; a bare id
list would need a second call. A named currency with zero rows in it (AC4) arises only from a deep link;
the handler test covers it. The partner NSwag regen is the lane's to run and report.

**Security (Gate 3) — `security_touching: true`, routed to the Security Reviewer beside the code
reviewer.** A response DTO on the partner host changes (`PeriodPaySummaryDto.AvailableCurrencies`); the
reviewer checks that the list is derived from the rows the handler already loaded for *this* employee and
period (the caller-is-the-employee check at `GetPeriodPays.cs:66-75` and the load at `:98-99` are
unchanged; no new read is added), so a cleaner cannot learn another cleaner's currencies through a period
id (S1), and that the new member carries currency ids and codes only.

## Status log

- 2026-09-19 — filed by the docs lane from the batch-6 panel; runs after T-0771 on the backend lane, then
  the partner web part. F6 on the owner plate points here.
- 2026-09-19 (review) — `todo`, no owner: it was filed `in_progress` for a lane that does not exist yet;
  no dependency, so it is ready the moment the backend lane is free. `security_touching` corrected to
  `true` — a response DTO changes, which is Gate 3's own definition; the routing note above says what the
  Security Reviewer looks at.

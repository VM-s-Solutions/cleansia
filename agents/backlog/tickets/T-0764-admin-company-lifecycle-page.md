---
id: T-0764
title: The admin app gets a Company lifecycle page — the state, the settlement facts with the date the seal becomes admissible, and the four acts behind confirmations
status: done
size: M
owner: —
created: 2026-09-16
updated: 2026-09-16
depends_on: [T-0760, T-0762, T-0763]
blocks: []
stories: []
adrs: [ADR-0064]
layers: [frontend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

ADR-0064 D4. The sibling shape is the Company settings page from T-0759 (facade in signals, `switchMap`
load, in-flight guard, sidebar gated by policy, five locales, a spec that reads the backend). Built
against the admin client regenerated after T-0763 (`87ff17e0`); nothing generated hand-edited. The
`lifecycle.chargeback_horizon_days` setting is already editable on Company settings — this page only
shows the resulting date.

## Doing (shipped `b18de583`)

- `libs/cleansia-admin-features/company-lifecycle`: route `/company-lifecycle` beside Company settings;
  the sidebar entry under `CanViewCompanyLifecycle`; two `CleansiaAdminRoute` entries; the five lifecycle
  policies in the frontend `Policy` mirror; `AdminCompanyLifecycleClient` on the aggregator.
- **State banner** — `Operating` / `WindingDown` / `Deactivated` / `Frozen` / `Archived` with a severity
  each, every stamp with the actor's e-mail (wind-down date + requested on/by, deactivated on/by, frozen
  on/by, archived on) and the manifest hash once archived.
- **Settlement facts** — the sixteen DTO members as a table (thirteen counts, the horizon date, last run,
  run in progress since): each an archive precondition or informational, `blocking` / `settled` /
  `informational` status; counts link to orders, pay periods, invoices or disputes where a list exists;
  the horizon row reads *archive admissible from ‹date›* (or *no horizon*) and links to Company settings.
- **Four acts** — *Deactivate* (danger), *Reactivate*, *Wind down* / *Run wind-down again* (a dialog with a
  date picker floored at today and the six consequences in the sweep's order; no date on a re-run),
  *Archive* / *Build archive again* — each a PrimeNG button gated by its policy and by the state table,
  disabled with a reason line that is the server's own refusal sentence (`api.company.archived`,
  `api.company.already_deactivated`, `api.company.operates_default_market`, `api.company.not_deactivated`,
  `api.company.wind_down_in_progress` with the one-hour staleness mirrored, `api.company.wind_down_not_requested`,
  the unsettled facts by name and count, the horizon date); each behind a confirmation stating exactly
  what it does — reactivate after a wind-down says the cancelled orders, ended Plus and discharged
  credit do not come back; archive says the books freeze at the click and retrieval is an operations
  step.
- **Facade** — the DTO, the act in flight and the dialog in signals; `load()` through `switchMap`; one
  act at a time; re-read after every act, admitted or refused, so the interceptor's `api.company.*` /
  `api.tenant.archived` sentence lands on a page that agrees with the server.
- **Specs** — the fact table and the act matrix per state against the generated DTO, the run staleness,
  the wire bodies, the confirm sentences, a refused act leaving the page consistent, the in-flight guard;
  a copy spec ties the five locales' state and fact names to the generated client's members.

## NOT

No bundle download or link (ops step until T-0748). No cross-company picker. No editing of the horizon
setting here. No audit-log label map. No customer or partner UI beyond the locale keys shipped in a/b/c.
No hand-edit of the generated client.

## Acceptance criteria

- [x] AC1 — with `CanViewCompanyLifecycle` the page shows the state and every fact from
      `GET api/AdminCompanyLifecycle/get`; without it the sidebar entry is absent and the route redirects.
- [x] AC2 — the act matrix per state: `Operating` not holding the default → Deactivate and Wind down
      enabled, Reactivate and Archive disabled with reasons; holding the default → Deactivate disabled
      with the reason; `Deactivated` with a date and non-zero facts → Reactivate and Run wind-down again
      enabled, Archive disabled naming every non-zero fact and the horizon; all zero and the horizon past
      → Archive enabled; a run in progress → Run wind-down again disabled; `Frozen` → only Build archive
      again; `Archived` → nothing, the hash shown.
- [x] AC3 — the wind-down dialog refuses a past date client-side and renders
      `company.wind_down_date_in_past` if it slips through; confirm sends one POST with the date, reloads,
      the button reads Run wind-down again.
- [x] AC4 — any of the twenty-one refusal keys shows its sentence, the page is re-read, no second request.
- [x] AC5 — a second act while one is in flight is ignored.
- [x] AC6 — five locales carry every key; the parity and DTO-member specs green; no hard-coded string;
      every control a wrapper or PrimeNG; `OnPush`; no `any`.

## Status log

- 2026-09-16 — shipped `b18de583`. Recorded in ADR-0064 D4; `/product/features` (Company lifecycle);
  `/admin-app/overview` (route, library, client); `/domain/roles/company-lifecycle`; CHANGELOG.

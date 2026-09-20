---
id: T-0782
title: Docs — ADR-0068 in docs/ with its trail, the role cards, business rules, flows, model, feature pages, the living companion, the plate and the questions
status: done
size: S
owner: —
created: 2026-09-20
updated: 2026-09-20
depends_on: [T-0777, T-0778, T-0779, T-0780, T-0781, T-0783, T-0784]
blocks: []
stories: []
adrs: [ADR-0068, ADR-0063]
layers: [docs]
security_touching: false
manual_steps: []
sprint: —
---

## Context

Docs are written at the end of a feature, once it is green. Every page below names the shipped shape
with the commit ids and was ground-truthed against the tree at `4cbd2c09` (the batch's head), not
against the panel text: where the tree departed from ADR-0068's draft, the departure is recorded in
the ADR's §*What shipped* and the pages describe the tree.

## Doing

- `docs/decisions/adr-0068.md` — the panel's final text with the Challenge / Defense / Verdict trail,
  `accepted` with a dated status line naming every commit, a §*What shipped* block; ADR-0063 D9's
  rendering sentence gets the *as amended* pointer; `docs/decisions/index.md` (68 records, the row,
  the no-arrow paragraph) and the VitePress sidebar gain the entry.
- `docs/domain/roles/work-contract-acceptance.md` (the three CRC cards of ADR-0068 §Roles in
  `legal-document.md`'s shape) + the roles index row; `tenant-configuration.md`'s counts (twelve keys,
  ten retention windows).
- `docs/product/business-rules.md` — *The contract for work* (`#work-contract`): the text the order
  is booked under, when the acceptance forms, what binds, one contract per seat, the admin path and the
  two gates, the three keys, what each party sees, the record, the retention row (the tenth window),
  the open questions.
- `docs/product/features.md` — customer, cleaner, admin; the settings counts.
- `docs/flows/offerability-and-take.md` (the take carries the acceptance; the admin path; the edge
  cases), `execution-and-completion.md` (`#the-contract-gate`), `booking-and-pricing.md` (the stamp),
  `cross-cutting.md` (the tenth sweep); `docs/domain/order-lifecycle.md` (one paragraph: `Confirmed`
  says nothing about the contract).
- `docs/domain/model.md` (both entities, the three FKs, 47 / 49 stamped), `docs/architecture/database.md`
  (the migration id `20260919231739`, 88 tables, the entity row), `docs/architecture/security-rules.md`
  (the new tenant-ignoring reads in ADR-0051's matrix), `docs/architecture/local-orchestration.md`
  (the Development boot seed, `efff863a`), `docs/api/orders.md` (the three endpoints, the take's
  fourteen-rule chain, the two gates), `docs/flows/gdpr-and-audit.md` (the twenty-first repository
  the erasure walks; the edge-case row), the customer / partner / admin overviews, the customer
  ordering flow, the partner and admin order-management pages and the mobile features page.
- `agents/architecture/decisions/work-contract.md` (the living companion); `agents/knowledge/patterns-backend.md`
  (the *acceptance echoes the text row it rendered* idiom, `T1-CI`).
- `CHANGELOG.md` — one `[Unreleased] / Added` entry in the reader's vocabulary (the customer, the
  cleaner, the admin, the API consumer, the operator), as ADR-0064/0065/0066 have.
- `agents/archive/2026-08/adr-deliberation/challenges/0068-contract-for-work.md` — the panel's C1–C17
  archived verbatim where `docs/decisions/index.md` §*Where the deliberation lives* says they are; the
  ADR's Challenge line names that path.
- `agents/backlog/tickets/T-0777…T-0784` filed with their commits (`done`, this one `in_progress`
  until AC1); INDEX rows + the batch note, *Next id* → T-0785; `agents/backlog/questions/open.md` gains Q-WC-01…07; `agents/OWNER-PLATE.md`
  (the Q-WC rows, the company-registration ruling as shipped, the findings, A1's runbook id, A2's
  draft); `agents/cleanup/MANUAL_STEPS.md` MS-2 and `agents/HANDOVER-2026-09-16.md` (the owed drop
  belongs to `20260919231739`).

## NOT

- No code; no ADR edits beyond the trail, the status line and the §What shipped block; no rewriting of
  ADR-0041 or ADR-0063 beyond D9's pointer. No CLAUDE.md edit (its "48 stamped tables" and "32
  component contracts" are stale — reported, not absorbed). No docs build run (the orchestrator runs
  the checkers).

## Done looks like

Every page above names the shipped shape with the commit ids; the ADR is `accepted` with its trail;
the plate and the queue carry the open questions and the findings; the docs build is green on the
orchestrator's run.

## Acceptance criteria

- [x] **AC1** — the docs build is green and the ADR renders at `/decisions/adr-0068` — **open; this is
      what keeps the row `in_progress`.** Neither docs pass had a shell. What closes it, run from the
      repo root by whoever holds one: `node agents/tools/check-docs-refs.mjs` (exit 0),
      `node agents/tools/check-catalog-claims.mjs` (exit 0 — `patterns-backend.md` changed),
      `cd docs && npm ci && npm run build` (green). Then tick this box with the three results, put the
      docs commit hash in the status log and the INDEX row, and flip both to `done`. What was checked
      by hand: every `](/…)` link in the ADR and the card resolves to a page in `docs/`; the two
      anchors (`#legal-seed`, `#s8-tenant-isolation-correctness`) match headings in the tree; no
      `→ /…` pointer in `src/` names a page or heading this ticket added or renamed.
- [x] **AC2** — `/product/business-rules#work-contract` states the rules and the retention row in the
      numbers-first style.
- [x] **AC3** — `/domain/roles/work-contract-acceptance` exists and the index lists it.
- [x] **AC4** — `questions/open.md` carries Q-WC-01…07 with their defaults and the ADR id.

## Status log

- 2026-09-20 — filed by the docs lane from the batch-9 panel; runs last, once every other lane is green.
- 2026-09-20 — the pages written in the docs commit that closes batch 9 (the orchestrator commits the
  path list this lane returned). The T-0783 record items were found folded into `a15d3af0`; the real
  admin-web commits are `2f3f5f79` + `27d9c5d9`; the model has **88** tables, not the 89 the ADR's
  consequence line projected — all three recorded in the ADR's §What shipped.
- 2026-09-20 — **review fixes; the row goes back to `in_progress`** because AC1 was unmet under a
  `done` status (the contradiction the INDEX's one-status rule exists to prevent) and this pass had no
  shell either. Fixed: the ADR's Challenge line names the archived challenge document instead of a
  scratchpad; the catalog entry carries its tier token; `CHANGELOG.md` has the entry; the ADR's §What
  shipped and the role card name the two gate suites (`StartOrderWorkContractGateTests`,
  `CompleteOrderWorkContractGateTests`, one file `WorkContractGateTests.cs`) rather than the file as a
  suite; the shared helper is cited at `libs/shared/utils/src/work-contract.utils.ts` in the ADR and
  on T-0781; the card's grep invariant states the tree's result (`AcceptedWorkContractTextId` → the two
  commands only; the acceptor takes `textId`), and the ADR's §What shipped records that departure from
  Verification #11. **The catalog search for the new entry's floor claim** (ADR-0033): `grep -n -i
  "echo|UserConsent|LegalDocumentText|termsAccepted|acceptance|LegalDocument|consent"
  agents/knowledge/patterns-backend.md` — every hit on point sits inside the new hunk (lines 279–302);
  outside it, line 274 (a registration's consent rows, the audit gate entry), 1105, 1175, 1512 and 1942
  are not about an act echoing a text row. No governing sentence existed; the entry is new, not a
  contradiction. Ratification of the entry is the Architect's (ADR-0033) — routed by the PM, not
  self-ratified here. The four named suites all live in `Cleansia.Tests` (the Postgres twin of
  `TakeOrderWorkContractTests` in `Cleansia.IntegrationTests`), both run by `backend-ci.yml`, so
  `T1-CI` is the right token.
- 2026-09-20 — done: the orchestrator ran `check-docs-refs.mjs` (266 references, 0 unresolved), `check-catalog-claims.mjs` (0 violations), `check-backlog-consistency.mjs` (OK) and the VitePress build (green) over the two docs passes and committed them as one `docs(...)` commit — the row closes on the build it names.

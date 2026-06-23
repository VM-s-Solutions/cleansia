---
id: T-0289
title: Per-detail-page drill-in entry points → the per-resource audit-history view
status: ready
size: S
owner: —
created: 2026-06-23
updated: 2026-06-23
depends_on: [T-0286]
blocks: []
stories: []
adrs: [0012]
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 11
---

> **No-decision note (panel skipped):** purely additive wiring of an EXISTING surface — the
> per-resource audit-history route + facade already shipped in T-0286
> (`audit-log/resource/:resourceType/:resourceId`). This ticket only adds the drill-in buttons that
> deep-link to it from the detail pages. No new product behavior, no new endpoint, no architectural
> decision (ADR-0012 is in force and untouched). Mechanical archetype-alignment against the existing
> audit-log feature.

## Context

ADR-0012 audit-log follow-up **(a)**, surfaced by T-0286's own close (T-0286 status log:
"embedding the per-resource history entry point into existing order/dispute/user detail pages —
deep-link route + facade are ready, drill-in is one nav call"). T-0286 shipped the per-resource
**history view** at `audit-log/resource/:resourceType/:resourceId` (gated by `Policy.CanViewAuditLog`
/ `adminGuard`) but **did not** wire any entry points into the existing admin detail pages, so today
an admin can only reach a resource's audit trail by navigating to the audit-log list and filtering by
`(ResourceType, ResourceId)` by hand. This ticket adds the one-click drill-in.

The audited resources with admin detail pages (ADR-0012's gated set): **order** (order-management /
order-ops detail), **dispute** (dispute-management detail), **user/employee** (user-management /
employee detail), and the **pay-config** edit surface. Each gets a "View audit history" affordance
that deep-links to the existing per-resource history route with the right `(resourceType, resourceId)`.

## Acceptance criteria

- [ ] **AC1 — Drill-in on each audited detail page.** Each existing admin detail page for an audited
  resource (order, dispute, user/employee; pay-config edit if it has a detail surface) gains a
  **"View audit history"** action that navigates to `audit-log/resource/:resourceType/:resourceId`
  with the page's own resource type + id. Uses `<cleansia-button>` (never raw `<button>`), routed via
  the existing route enum — no hand-built URL strings.
- [ ] **AC2 — Permission-gated, mirroring the audit-log nav.** The drill-in is shown only when the
  admin holds `Policy.CanViewAuditLog` (`*cleansiaPermission="Policy.CanViewAuditLog"`), matching the
  gate T-0286 put on the audit-log nav item and route. An admin without the policy never sees the
  button.
- [ ] **AC3 — Correct resource mapping.** The `resourceType` constant each page passes matches the
  backend `ResourceType` the audit behavior records for that entity (verify against T-0283/T-0284's
  recorded types — order / dispute / user / pay-config) so the history view actually filters to rows.
  A mismatched type that yields an empty history is a defect.
- [ ] **AC4 — i18n ×5.** The button label (and any tooltip) uses `TranslatePipe` with keys present in
  **all 5** admin locales (en, cs, sk, uk, ru). No hardcoded strings.
- [ ] **AC5 — Mechanical checks green.** `nx test` for each touched feature lib + the audit-log lib
  passes; `nx build cleansia-admin.app --configuration=production` is clean;
  `check-consistency.mjs` reports no new violation.

## Out of scope
- **No new backend endpoint / DTO / migration** — reuses the T-0285 query + the T-0286 history facade
  exactly as shipped.
- **The single-row before/after diff view** — that is the sibling follow-up **T-0290** (it needs a new
  backend endpoint and is deliberately NOT this ticket).
- **No change to the audit-log list/history feature itself** — only the entry points into it.
- **Customer/partner surfaces** — audit history is an admin-only oversight surface.

## Implementation notes
Mirror how T-0286 routed the history view (`audit-log/resource/:resourceType/:resourceId`,
`Policy.CanViewAuditLog`, gated nav). The drill-in is "one nav call" per the T-0286 note — inject the
router (or the audit-log feature's nav helper if one exists) and push the route with the page's
resource type+id. Add the button to each detail component's existing action area using the established
`<cleansia-button>` + `*cleansiaPermission` pattern. Confirm the `resourceType` literals against the
backend (T-0283/T-0284) before wiring — the history filters on the exact recorded type.

**Routing:** `[frontend]`. `reviewer`-per-dev. `qa` = the per-resource history opens from each detail
page with the correct rows + the gate hides it without the policy. No `security` gate
(`security_touching: false` — it adds no endpoint/authz; the route it links to is already gated). No
`optimizer`.

## Status log
- 2026-06-23 — draft → ready (created by pm). ADR-0012 follow-up **(a)**, surfaced in T-0286's close
  and **never previously ticketed** (verified: highest pre-existing id was T-0288). DoR met: AC
  observable; sized **S** (additive drill-in on a handful of existing detail pages, no new
  pattern/endpoint); `depends_on: [T-0286]` (the history route/facade it links to — `done`);
  `layers: [frontend]`; `security_touching: false`; `manual_steps: []`; archetype = the T-0286
  audit-log feature + the existing `<cleansia-button>` + `*cleansiaPermission` detail-page pattern.
  No panel (ADR-0012 accepted; purely additive wiring).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->

---
id: T-0290
title: Single-row before/after audit diff view + new single-row backend endpoint (off the initial PII-min cut)
status: ready
size: M
owner: —
created: 2026-06-23
updated: 2026-06-23
depends_on: [T-0284, T-0285, T-0286]
blocks: []
stories: []
adrs: [0012]
layers: [backend, frontend]
security_touching: true
manual_steps: [nswag-regen]
sprint: 11
---

> **No-decision note (panel skipped — but security-gated):** the *decision* to keep `BeforeJson`/
> `AfterJson` off the initial list/history cut for PII-minimization is **already made and ratified**
> in ADR-0012 (D4.1) + Q-AUDIT-01 (the owner's "snapshots store ids + changed fields only, never raw
> subject PII" default). This ticket implements the **deliberately-deferred** single-row diff read of
> those already-pre-redacted snapshots; it does not reopen the PII policy. No new product behavior
> beyond exposing an existing column via a narrow, authz-gated single-row endpoint. Security gate is
> mandatory because it surfaces snapshot blobs the list cut intentionally withheld.

## Context

ADR-0012 audit-log follow-up **(b)**, surfaced by T-0286's close (T-0286 status log: "a row
drill-down for the before/after JSON diff — the list/history projection omits `BeforeJson`/`AfterJson`
for PII-minimization per ADR-0012 D4.1, so a diff view needs a new single-row backend endpoint (own
ticket)"). T-0284 records typed, **pre-redacted** before/after snapshots (ids + changed fields only,
never raw subject PII) on the five sensitive actions; T-0285's `GetPagedAdminActionAudits` query +
T-0286's list/history UI **deliberately project those blobs out** so a bulk list never streams snapshot
payloads. This ticket adds the **single-row** read: an admin clicks one audit row and sees its
before→after field diff.

Because the snapshots are already PII-minimized at write time (T-0284), the diff view does not perform
new redaction — it renders the stored pre-redacted `BeforeJson`/`AfterJson`. The **new surface** is a
narrow, `AdminOnly`-gated **single-row** endpoint returning one audit row *with* its snapshot blobs
(distinct from the paged query, which omits them), plus a read-only admin diff UI.

## Acceptance criteria

- [ ] **AC1 — New single-row endpoint, gated by the existing audit view policy.** A
  `GetAdminActionAuditById` query (canonical handler/validator) returns **one** `AdminActionAudit` by
  id, **including** its `BeforeJson` / `AfterJson` snapshot fields, gated by the **same** `AdminOnly`
  view policy T-0285 added (e.g. `CanViewAuditLog` → `AdminOnly`). Tenant-scoped (global filter
  applies; a row from another tenant returns not-found). No new policy is introduced.
- [ ] **AC2 — Snapshots returned ONLY on the single-row read.** The paged `GetPagedAdminActionAudits`
  query stays **unchanged** — it still omits the snapshot blobs. Only this single-row endpoint returns
  them, so a bulk list never streams snapshot payloads (the ADR-0012 D4.1 projection discipline holds).
  A test asserts the paged DTO has no `before/after` and the single-row DTO does.
- [ ] **AC3 — No new redaction / no raw subject PII.** The endpoint returns the **already-pre-redacted**
  snapshots T-0284 stored (ids + changed fields only); it performs no new PII handling and exposes no
  field the write-side didn't already minimize. A reviewer/security check confirms the DTO carries only
  what T-0284 persisted — the GDPR-survives-erasure rows still show actor + scope + subject id, never
  the erased subject's personal data.
- [ ] **AC4 — Read-only admin diff UI.** From the audit-log list/history (T-0286), an admin opens a
  single row and sees a **read-only before/after field diff** (changed fields highlighted), rendered in
  the existing `audit-log` admin feature lib via the generated client (never raw `http.*`, never a
  hand-edited NSwag client). Three explicit data states (loading / loaded / empty-or-error). OnPush,
  no `any`.
- [ ] **AC5 — i18n ×5.** Every user-visible string (the diff headings, "no changes recorded" empty
  state, field labels where applicable) uses `TranslatePipe` with keys in **all 5** admin locales.
- [ ] **AC6 — Gates green incl. real-DB + security.** Backend: `GetAdminActionAuditById` handler +
  validator unit tests + an **authz-rejection integration test** (non-admin / cross-tenant → rejected/
  not-found) against real Postgres (`IntegrationTests`/`HostTests`, not just the unit suite). Frontend:
  `nx test audit-log` + `nx build cleansia-admin.app --configuration=production` clean (against the
  regenerated client). `check-consistency.mjs` no new violation (the query passes A1/A5). **Security
  gate** signs off the snapshot-exposure surface.

## Out of scope
- **No change to the PII-minimization policy** — ADR-0012 D4.1 / Q-AUDIT-01 stand; this reads the
  existing pre-redacted snapshots, it does not widen what is stored.
- **No change to the paged list/history projection** (T-0285/T-0286) beyond linking into the new
  single-row view — the list keeps omitting the blobs (AC2).
- **No write/mutation of audit rows** — append-only, read-only UI.
- **The drill-in entry points from detail pages** — that is the sibling follow-up **T-0289**.
- **No retention/prune change** — the audit table stays no-auto-delete (T-0287 excludes it).

## Implementation notes
Read ADR-0012 **D4.1** (snapshot projection discipline — why the list omits the blobs) and **D7**
(frontend surface), and the Q-AUDIT-01 answer (PII-min default). Backend: mirror the canonical
single-entity query shape; reuse T-0285's `AdminOnly` policy (do **not** add a new policy — that file
cluster is owned/serialized). The new DTO carries the snapshot fields → **owner nswag-regen (admin)**
is required before the frontend half can build (flag it; this ticket is **held from `done`** until the
regen lands + the admin prod-build is clean, the same gate T-0286 used). Frontend: extend the existing
`audit-log` feature lib with a single-row diff view reusing its facade/signals pattern. **Security
gate mandatory** — the seam exposes the snapshot blobs the list cut intentionally withheld.

**Routing:** lock the contract first — `[backend]` (query + DTO + tenant/authz tests) → owner regen →
`[frontend]` (diff view). `reviewer`-per-dev on both. `security` gate on the backend endpoint
(snapshot exposure + tenant/authz). `qa` = AC↔evidence + the authz-rejection + the paged-vs-single
projection assertion (AC2). No `optimizer` (single-row read).

## Status log
- 2026-06-23 — draft → ready (created by pm). ADR-0012 follow-up **(b)**, surfaced in T-0286's close
  and **never previously ticketed** (verified: highest pre-existing id was T-0288). DoR met: AC
  observable; sized **M** (one narrow backend query+DTO+tests + one read-only FE view, no new
  pattern); `depends_on: [T-0284, T-0285, T-0286]` (the snapshots, the query/policy, the feature lib —
  all `done`); `layers: [backend, frontend]`; **`security_touching: true`** (surfaces the snapshot
  blobs the list cut withheld → mandatory security gate); `manual_steps: [nswag-regen]` (new DTO →
  owner admin regen; held from `done` until confirmed). Archetype = canonical single-entity query +
  the T-0286 audit-log feature lib. No deliberation panel — the PII-min decision is already
  ADR-0012/Q-AUDIT-01-ratified; this is the deferred read of an already-decided contract. **Owner
  manual step:** admin nswag-regen for the new single-row snapshot DTO (batch with any other pending
  admin regen).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->

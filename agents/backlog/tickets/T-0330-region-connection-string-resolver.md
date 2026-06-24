---
id: T-0330
title: Connection-string resolver indirection (ADR-0017 region seam — the data-layer seam, code only)
status: done
size: S
owner: backend
created: 2026-06-23
updated: 2026-06-23
depends_on: []
blocks: []
stories: []
adrs: [0017]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 13
---

> **No-decision note:** the *decision* (lay a `region → connection-string` resolver indirection now,
> defer the `CountryConfiguration.HomeRegion` column to first-second-region work) is already made and
> ratified in **ADR-0017** (the region seam). This ticket implements that ratified seam as a small
> behavior-preserving code change — no new pattern, no schema change. No deliberation panel.

## Context

ADR-0017 (multi-region expansion seam). The region seam needs **one place** the DB connection string is
resolved, so that per-region DBs later are a **resolver change, not an app rewrite**. Today the resolver
returns the **single shared** West-Europe DB — behavior-preserving. **No schema change** (the
`CountryConfiguration.HomeRegion` column is DEFERRED to first-second-region work → keeps this wave
migration-free). The hard invariant: the **tenancy filter is untouched** — region is infra/config and
orthogonal to the app-level row-scoped `TenantId` filter; conflating them is the failure mode ADR-0017
exists to prevent.

## Acceptance criteria

- [x] **AC1 — One resolution point.** An `IRegionConnectionStringResolver` +
  `RegionConnectionStringResolver` is the **single** place the DB connection string is chosen; today it
  returns the single shared DB. Evidence:
  `src/Cleansia.Infra.Common/Configuration/Interfaces/IRegionConnectionStringResolver.cs` (13 lines) +
  `src/Cleansia.Infra.Common/Configuration/RegionConnectionStringResolver.cs` (22 lines);
  `DbContextBindingExtensions` routes through it.
- [x] **AC2 — Behavior-preserving.** No handler/repo hard-codes a region or reaches a second connection
  string; the resolution is the one indirection, returning today's shared connection. Evidence:
  `dotnet build Cleansia.Config` 0 errors; grep — no second hard-coded connection string.
- [x] **AC3 — Tenancy filter UNTOUCHED.** `CleansiaDbContext.ApplyTenantQueryFilters` (and the tenancy
  code) carries **no** region clause — region is not in the row filter. Evidence: the tenancy-filter diff
  is empty (orchestrator confirmed).
- [x] **AC4 — No schema change.** No ef-migration; the `CountryConfiguration.HomeRegion` column is NOT
  added (deferred). `manual_steps: []`.

## Out of scope

- The `CountryConfiguration.HomeRegion` column — DEFERRED to first-second-region work (an owner
  ef-migration then, not now).
- Provisioning a second region / a second DB — explicitly not this wave (single-region).
- Any change to the tenancy filter — forbidden (AC3); region must not enter the row filter.

## Implementation notes

Read ADR-0017 (the data-layer seam, R6/R7). Introduce the resolver as the one indirection
`DbContextBindingExtensions` calls; return the existing single shared connection. Do **not** touch
`CleansiaDbContext.ApplyTenantQueryFilters`. Reviewer ADR-0017 R6 (the resolver is the single connection-
string source; no handler hard-codes a region/second connection string), R7 (tenancy filter UNCHANGED —
a region clause in `ApplyTenantQueryFilters` is a conflation finding), R8 (no handler branches on a
region code) apply.

> **Reporting-vs-work note (this ticket):** the in-workflow dev agent's final **StructuredOutput** report
> call hit the retry cap and errored — a **reporting** failure, not a work failure. The resolver +
> `DbContextBindingExtensions` change had already landed on disk. Per `quality-gates.md` §"A final-report
> (StructuredOutput) failure ≠ a work failure", the orchestrator **gated the on-disk result by hand**:
> read the resolver, built `Cleansia.Config` (0 errors), confirmed the tenancy filter diff is empty + the
> connection string is chosen in exactly one place. **Verified-done by the hand gate even though the
> in-workflow reviewer instance didn't run its final report.**

## Status log

- 2026-06-23 — draft → ready (created by pm). One-line **no-decision** note (the seam is ADR-0017-
  ratified; mechanical implementation, no new behavior/decision). DoR met: AC observable; sized `S`;
  `depends_on: []`; `layers: [backend]`; `security_touching: false`; `manual_steps: []` (no
  ef-migration — code seam only). Runnable on approval with no Azure access (parallel to the Bicep work).
- 2026-06-23 — ready → in_progress → in_review → **done** (implemented + **hand-gated by the
  orchestrator**; commit `38a10375`, pushed). **All four AC satisfied.** `IRegionConnectionStringResolver`
  + `RegionConnectionStringResolver` are the single resolution point (today → the shared West-Europe DB);
  `DbContextBindingExtensions` routes through it; behavior-preserving; **no schema change** (`HomeRegion`
  deferred). **VERIFICATION NOTE:** the dev agent's final StructuredOutput report call **failed (retry
  cap)** — a reporting failure, NOT a work failure (the resolver landed on disk). The orchestrator **gated
  it BY HAND**: read the resolver, ran `dotnet build Cleansia.Config` → **0 errors**, confirmed the
  tenancy filter (`CleansiaDbContext.ApplyTenantQueryFilters`) diff is **empty** (untouched), confirmed
  one resolution point (no second hard-coded connection string, no handler branching on a region code).
  **Verified-done** despite the in-workflow reviewer not emitting its final report. Process lesson
  reinforced in `quality-gates.md`.

## Review

## Review — orchestrator hand-gate (2026-06-23, in lieu of the in-workflow reviewer whose StructuredOutput failed)

- ADR-0017 R6 (one resolution point; no handler hard-codes a region/second connection string): PASS —
  `RegionConnectionStringResolver` is the single source; `DbContextBindingExtensions` routes through it;
  grep confirms no second hard-coded connection string.
- ADR-0017 R7 (tenancy filter UNCHANGED): PASS — `CleansiaDbContext.ApplyTenantQueryFilters` diff is
  empty; no region clause entered the row filter.
- ADR-0017 R8 (no handler branches on a region code): PASS.
- Gate 8 Mechanical: `dotnet build Cleansia.Config` → 0 errors. No ef-migration (code seam only).

Verdict: APPROVED (hand-gate). The in-workflow reviewer's final StructuredOutput call failed (retry cap)
— a reporting failure; the work was on disk and is gated here by hand. Behavior-preserving, tenancy
untouched, single resolution point.

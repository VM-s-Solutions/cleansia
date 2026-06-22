---
id: T-0274
title: Dedup the API error-key extractor across 8 feature facades onto one shared @cleansia/services helper
status: ready
size: M
owner: —
created: 2026-06-22
updated: 2026-06-22
depends_on: []
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 9
---

> **No-decision note (panel skipped):** mechanical DRY consolidation of an already-canonical idiom onto
> its existing canonical home (`SnackbarService` in `@cleansia/services`). No new behavior — each facade
> resolves the same error key from the same API error as today; only the duplicated extraction code moves.

## Context

Audit finding #1 (HIGH). The shared "pull the error code out of an ASP.NET ProblemDetails / response
string" logic is re-implemented privately in **8 feature facades**, each with its own local
`ApiErrorResult` interface and a `private resolveErrorKey(error)` that duplicates the
`result.detail || result.title` → `JSON.parse(response)` extraction before a feature-specific map
lookup. Verified shape (`admin-order-ops.facade.ts:190-207`):
```
const apiError = error as { result?: ApiErrorResult; response?: string };
let code = apiError?.result?.detail || apiError?.result?.title;
if (!code && apiError?.response) { try { code = (JSON.parse(...)).detail || ...title; } catch {...} }
return ORDER_OPS_ERROR_KEY_MAP[code] ?? ORDER_OPS_FALLBACK_ERROR_KEY;
```

The **canonical extraction** already lives in `SnackbarService.extractApiErrorMessage`
(`libs/core/services/.../snackbar.service.ts:117-165`) — the same `result.detail || result.title` →
`JSON.parse(response)` walk. The canonical *resolver idiom* (a per-feature code→key map) is exemplified
cleanly in `membership-plan-list.models.ts:143-166` and `referrals-list.models.ts:56-77`, which already
keep only a map and delegate extraction.

**The 8 private copies to collapse:**
1. `admin-order-ops.facade.ts:190-207`
2. `admin-order-refund.facade.ts:118-135`
3. `dispute-detail.facade.ts:166-183` (admin disputes-management)
4. `admin-pay-period-ops.facade.ts:118-135`
5. `admin-payroll-ops.facade.ts:199`
6. `invoice-management.facade.ts`
7. `package-form.facade.ts` (admin package-management)
8. customer `disputes.facade.ts:259-276`

## Acceptance criteria

- [ ] **AC1 — Characterization-test-first.** Before consolidation, each of the 8 facades' error-key
  resolution is pinned by a facade unit test (add where missing) covering: code from `result.detail`,
  code from `result.title` fallback, code from a JSON `response` string, the feature map hit, and the
  fallback key when no code matches. These tests stay **green unchanged** through the refactor (proves
  behavior preserved).
- [ ] **AC2 — One shared extractor.** A single `extractApiErrorCode(error): string | undefined` (the
  pure code-extraction half — the `result.detail || result.title` → `JSON.parse(response)` walk) is
  exposed from `@cleansia/services` (alongside / extracted from `SnackbarService`), typed with the
  **one** shared `ApiErrorResult` type (the 8 local interface copies are deleted). No `any`.
- [ ] **AC3 — Each facade keeps only its map.** Each of the 8 facades retains a thin
  `resolveXxxErrorKey(error)` that calls the shared `extractApiErrorCode` and applies its **own**
  feature code→key map + fallback. The private extraction copies and local `ApiErrorResult` interfaces
  are removed. The resolved key for any given API error is **identical** to before (the maps are
  unchanged).
- [ ] **AC4 — No behavior/UX drift.** No user-visible message changes; every error still resolves to
  the same `errors.*` / `api.*` translation key it did before. The fallback behavior (unknown code →
  feature fallback key) is preserved per facade.
- [ ] **AC5 — Mechanical checks green.** The affected app(s) `nx build` (production) + `nx affected -t test`
  (Jest) pass; `check-consistency.mjs` reports no new violation in the touched libs.

## Out of scope
- **The `.models.ts` resolver maps** that already delegate (`membership-plan-list.models.ts`,
  `referrals-list.models.ts`, etc.) — they are the canonical idiom; leave them. Only the **8 private
  extraction re-implementations** collapse.
- **No new error keys, no i18n changes, no map content changes** — the maps move/stay verbatim.
- **`SnackbarService.extractApiErrorMessage`** keeps its existing public behavior (it may be refactored
  to *use* the new shared `extractApiErrorCode` internally, but its message-building contract is unchanged).
- **No facade restructure** beyond removing the duplicated extractor.

## Implementation notes

Single ticket, but **8 files across 6 libs** → fan out is optional: one frontend dev can do it serially
(it's mechanical and the shared helper is the only cross-file coupling), with one reviewer. If split,
the shared `@cleansia/services` helper (AC2) must land first; then the 8 facade edits parallelize (each
facade is a disjoint file). **Serialize on `snackbar.service.ts`** — only the shared-helper task edits it.

`security_touching: false` (no endpoint/authz/DTO change; pure FE refactor over existing error payloads).

**Routing:** `[frontend]`. `reviewer`-per-dev. `qa` = Jest green + AC1↔test mapping (the resolution is
identical for each facade's representative error codes). No `security`, no `optimizer` (not a hot path).

## Status log
- 2026-06-22 — draft → ready (created by pm). Finding #1 VERIFIED: `resolveErrorKey` re-impl confirmed
  in `admin-order-ops.facade.ts:190-207`; canonical `extractApiErrorMessage` confirmed in
  `snackbar.service.ts:117-165`. 8 facades, 6 libs. No-decision (DRY onto existing canonical home).
  `manual_steps: []`. Sized **M** (8 files, behavior-preserving under per-facade characterization nets;
  if the shared-type extraction balloons past M, stop and split the helper from the consumers).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->

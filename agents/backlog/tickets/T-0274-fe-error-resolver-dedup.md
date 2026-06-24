---
id: T-0274
title: Dedup the API error-key extractor across 8 feature facades onto one shared @cleansia/services helper
status: done
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
- 2026-06-22 — ready → review (frontend). Done serially within M; did not hit STOP-AND-SPLIT.
  **AC2:** new `extractApiErrorCode(error): string | undefined` + the one shared `ApiErrorResult` type in
  `libs/core/services/src/lib/services/api-error.ts`, exported from `@cleansia/services`. The pure
  `result.detail || result.title` → `JSON.parse(response)` walk; normalizes empty/missing to `undefined`
  (downstream `if (code && MAP[code])` is unchanged-equivalent — empty string and undefined both fall
  through to the feature fallback, as before). `SnackbarService` now consumes it and its private
  `ApiErrorResult` copy is deleted; its non-JSON-response literal branch is preserved (re-parse guard,
  byte-equivalent — pinned by a new `snackbar.service.spec.ts`, 10 cases incl. `'{}'`/HTML/title/mapping).
  **AC3:** all 8 facades now keep only their thin `resolveXxxErrorKey` (own map + fallback) delegating to
  the shared helper; the 8 private extraction copies + 8 local `ApiErrorResult` interfaces are removed
  (grep confirms 0 `interface ApiErrorResult` outside `api-error.ts`, 0 `JSON.parse`/`result?.detail`
  extraction left in the 8). Maps untouched. **AC1:** added the missing `result.title`-fallback
  characterization case to all 8 facade specs (and the missing JSON-`response` case to invoice-mgmt,
  package-form, customer-disputes); confirmed GREEN against the un-refactored facades first, still green
  after. **AC4:** identical key resolves for the same error — every facade error-resolution test passes
  unchanged. **AC5:** `nx test` green for services + all 6 affected libs (facade specs); `cleansia-admin.app`
  and `cleansia.app` (SSR) **production builds succeed**; `check-consistency.mjs` reports **0** violations in
  any touched file (8 facades + `api-error.ts` + `snackbar.service.ts`); lint clean on every touched file.
  Harvested the canonical rule into `patterns-frontend.md` (per-feature resolvers must delegate extraction
  to `extractApiErrorCode`). **Deviations (not mine, pre-existing/concurrent-lane):**
  `order-management.component.spec.ts` "No provider for _HttpClient" fails identically on the stashed tree
  (proven by git-stash) — outside this ticket's scope (it's the list component, not the ops facade);
  other pre-existing lint findings (`invoice-detail.facade.ts` module-boundary, a11y, non-null assertions,
  services circular-dep) are in untouched files. **manual_steps: []** (no DTO/response-shape change → no
  nswag-regen). Not committed/pushed.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
- **Catalog harvest (please sanity-check):** added a one-line clarification to
  `patterns-frontend.md` (§"Other (non-canonical) error-resolution paths") that per-feature
  `resolveXxxErrorKey` resolvers must delegate code extraction to the shared `extractApiErrorCode`
  rather than re-implement the walk inline. Small clarification to an existing rule, not a new archetype.
- **Out-of-scope note for a follow-up (not done here):** the canonical `.models.ts` resolvers
  (`membership-plan-list.models.ts:56-79`, `referrals-list.models.ts:56-79`,
  `admin-user-form.models.ts`, `service-management.models.ts`, `package-management.models.ts`,
  `currency-management.models.ts`, `admin-profile.models.ts`) STILL inline the same extraction walk —
  the ticket explicitly scoped them out. They could now also delegate to `extractApiErrorCode` in a
  future DRY pass; flagging so the convergence isn't forgotten.

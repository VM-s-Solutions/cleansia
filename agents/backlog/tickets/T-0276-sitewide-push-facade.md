---
id: T-0276
title: Extract a SitewidePushFormFacade — move HTTP/state off the component onto the generated client
status: done
size: S
owner: frontend
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

> **No-decision note (panel skipped):** brings one stray component into line with the ratified
> facade-owns-logic pattern (`patterns-frontend.md`). No new behavior — same POST, same confirm, same
> success/error UX; the logic just moves component → facade and the raw `http.post` becomes the
> generated client call.

## Context

Audit finding #10 (MED). `sitewide-push-form.component.ts` keeps HTTP, error, confirm-dialog and
in-flight state **in the component** with no facade, and posts via a **raw `http.post`** to a
hand-built URL (`:131-155`):
```
this.http.post(`${this.apiBaseUrl}/api/AdminMarketing/send-sitewide-promo`, this.form.getRawValue(), { withCredentials: true })
```
It uses `DestroyRef`/`takeUntilDestroyed` (`:125,143`) instead of the canonical
`UnsubscribeControlDirective` + `takeUntil` that every other form facade uses. The generated
`AdminMarketingClient.sendSitewidePromo` **already exists** in `admin-client.ts` (verified), so no regen
is required to adopt it — unless the dev finds the `SendSitewidePromo` request DTO shape drifted from
the form's `getRawValue()` (then it's a conditional regen flag — see AC5).

## Acceptance criteria

- [ ] **AC1 — Facade-first (test before move).** A `SitewidePushFormFacade` extending
  `UnsubscribeControlDirective` is created with a facade unit test written first, covering: submit →
  confirm → send happy path, the confirm-cancelled path (no send), and the error path (error snackbar,
  `submitting` reset). The component delegates submit/send/state to the facade.
- [ ] **AC2 — Generated client, not raw http.** The send uses
  `inject(AdminClient).adminMarketingClient.sendSitewidePromo(...)` (the existing generated client),
  not `http.post` to a hand-built URL. The hand-built `apiBaseUrl` string is removed.
- [ ] **AC3 — RxJS cleanup canonical.** State flows through the facade with `takeUntil` (via
  `UnsubscribeControlDirective`); the component no longer owns `DestroyRef`/`takeUntilDestroyed` for the
  send/confirm pipelines.
- [ ] **AC4 — UX identical.** Confirm dialog copy/keys, success snackbar, error snackbar, form reset,
  and the disabled/`submitting` button state are **unchanged** to the user. The "fan-out runs
  unstoppable once enqueued" confirm semantics are preserved.
- [ ] **AC5 — Mechanical checks green + regen honesty.** Admin app `nx build` (production) +
  `nx affected -t test` pass; `check-consistency.mjs` no new violation. If — and only if — the dev finds
  the generated `sendSitewidePromo` request DTO does not match the form payload (a real backend DTO
  drift), they **stop and flag `manual_steps: [nswag-regen]` to the PM** rather than hand-shaping a
  client; this ticket ships `manual_steps: []` and stays that way unless that flag is raised.

## Out of scope
- **No backend change** to `SendSitewidePromo` — this consumes the existing endpoint.
- **No new validation rules, no new locale keys** — the form/validators/messages are unchanged.
- **No marketing-feature restructure** beyond extracting the facade.

## Implementation notes

Mirror any existing admin form facade (e.g. an admin-*-ops facade) for the `UnsubscribeControlDirective`
+ `takeUntil` + signal-state shape. **Single frontend dev + one reviewer**, serial (one feature folder).

**Routing:** `[frontend]`. `reviewer`. `qa` = Jest green + AC4 UX-parity confirmation. No `security`
(consumes an existing admin endpoint behind the same auth; no DTO/authz change), no `optimizer`.

## Status log
- 2026-06-22 — draft → ready (created by pm). Finding #10 VERIFIED: component does raw `http.post` +
  component-owned state at `sitewide-push-form.component.ts:131-155`; `AdminMarketingClient` /
  `sendSitewidePromo` already present in `admin-client.ts` (so adoption needs no regen). No-decision
  (pattern alignment). `manual_steps: []` (conditional regen flag only if DTO drift is found — AC5).
  Sized **S**.
- 2026-06-22 — ready → review (frontend). Extracted `SitewidePushFormFacade extends
  UnsubscribeControlDirective` owning confirm → send → signal state via `takeUntil(this.destroyed$)`;
  component now delegates and keeps only the form + invalid-guard (UI). Send goes through the generated
  `adminClient.adminMarketingClient.sendSitewidePromo(command)`; raw `http.post`, `apiBaseUrl`,
  `HttpClient`, `DestroyRef`/`takeUntilDestroyed` all removed.
  - **DTO drift check (AC5):** none. Generated `SendSitewidePromoCommand` fields (titleEn..bodyRu) match
    the form's `getRawValue()` exactly, and the client posts the same URL
    (`/api/AdminMarketing/send-sitewide-promo`). `manual_steps: []` — **no nswag-regen needed**.
  - The generated `AdminMarketingClient` existed in `admin-client.ts` but the **hand-maintained**
    `AdminClient` wrapper (`admin-base-client.ts`) did not expose it; added `adminMarketingClient`
    (import + interface member + field). The wrapper/barrel is hand-maintained (NSwag only emits
    `admin-client.ts`), so this is normal frontend work, **not** a regen step (per `patterns-frontend.md`).
  - **Test-first (AC1):** wrote `sitewide-push-form.facade.spec.ts` first (happy path confirm→send→
    success+onSuccess; confirm-cancelled = no send; error path = error snackbar + `submitting` reset;
    in-flight `submitting` toggles). The marketing lib had no Jest harness, so added the standard Nx
    scaffolding (`jest.config.ts`, `tsconfig.json`/`.lib.json`/`.spec.json`, `src/test-setup.ts`, `test`
    + `lint` targets in `project.json`) mirroring `disputes-management`.
  - **UX parity (AC4):** identical confirm keys (`pages.sitewide_push.confirm_send` /
    `…confirm_title`), success (`…send_success`), error (`…send_error`), validation
    (`…validation_error`), `form.reset()` on success, and `submitting` button state. No new locale keys,
    no validator changes.
  - **Evidence:** `nx test marketing` 4/4 PASS; `nx lint marketing` clean; `nx build cleansia-admin.app
    --configuration=production` SUCCESS (2 pre-existing warnings in `employee-detail` + bundle budget,
    unrelated). `check-consistency.mjs`: facade C3 cleared (100 → 97 total); the only remaining marketing
    flag is a **pre-existing** D2 (`fb.group` vs `fb.nonNullable.group`) that was already at HEAD and is
    out of scope (form unchanged).
  - **Pre-existing (not mine):** `order-management.component.spec.ts` fails (`No provider for _HttpClient`)
    and `admin-services` lint fails (circular dep `admin-services ↔ admin-stores` in `loading.interceptor.ts`)
    — both proven identical against the HEAD baseline wrapper (concurrent lanes' WIP). Not caused by this
    change.
  - **Manual steps:** none.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->

### Reviewer verdict — 2026-06-22 — APPROVED

Verified-not-trusted. All ACs met; behavior-preserving.

- **AC1 (facade-first, tested):** `SitewidePushFormFacade extends UnsubscribeControlDirective`; spec
  covers happy / confirm-cancelled / error+submitting-reset / in-flight. `nx test marketing` 4/4 PASS
  (ran).
- **AC2 (generated client, not raw http):** send goes through
  `adminClient.adminMarketingClient.sendSitewidePromo(command)`; generated client posts the same URL
  `/api/AdminMarketing/send-sitewide-promo` (admin-client.ts:7245) with matching 10-field
  `SendSitewidePromoCommand` (titleEn..bodyRu). `apiBaseUrl`/`HttpClient` removed.
- **AC3 (RxJS canonical):** `takeUntil(this.destroyed$)` in the pipe; component no longer owns
  `DestroyRef`/`takeUntilDestroyed`.
- **AC4 (UX identical):** template unchanged (no diff); same confirm/success/error/validation keys,
  `form.reset()`, `submitting()` loading+disabled binding. The original inline `withCredentials:true`
  is preserved by `admin-services AuthInterceptorFn` (sets withCredentials + CSRF on every state-changing
  API call), so the cookie/CSRF flow is intact.
- **AC5 (mechanical + regen honesty):** `nx build cleansia-admin.app --configuration=production` SUCCESS
  (only pre-existing employee-detail optional-chain + bundle-budget warnings, unrelated);
  `nx lint marketing` clean; `check-consistency.mjs` shows only the **pre-existing** D2 `fb.group`
  (existed at HEAD line 69, form out of scope — noted, not blocking). No DTO drift → `manual_steps: []`
  correct. The `admin-base-client.ts` wrapper edit is hand-maintained-barrel work, not a regen step
  (patterns-frontend.md).

Comment discipline clean (zero comments in new facade/component; stale "swap once regen lands" block
removed; no T-NNNN refs). Encoding clean (no BOM, no mojibake). Scope clean (only the listed
marketing + wrapper files; snackbar/dialog APIs pre-existed at HEAD).

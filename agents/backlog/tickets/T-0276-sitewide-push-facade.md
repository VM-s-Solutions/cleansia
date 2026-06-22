---
id: T-0276
title: Extract a SitewidePushFormFacade — move HTTP/state off the component onto the generated client
status: ready
size: S
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

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->

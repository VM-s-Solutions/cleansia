---
id: T-0264
title: Remove vestigial api.email.sending_failed locale keys (admin.app + partner.app, ×5 locales each)
status: done
size: S
owner: pm
created: 2026-06-15
updated: 2026-06-21
depends_on: [T-0262]
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 7
source: T-0262 residual — its errors.*/BusinessErrorMessage scope did not cover the api.* locale namespace
---

## Context
**T-0262** removed the dead backend constant `BusinessErrorMessage.EmailNotSentError` (zero consumers).
Its scope was the backend constant + the `errors.*` locale namespace, which did **not** reach the
`api.*` namespace. The frontend mirror of that error therefore survives as a vestigial key
`api.email.sending_failed` in two apps, five locales each (10 entries total):

- `apps/cleansia-admin.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` → `api.email.sending_failed`
  (e.g. en: "Failed to send email")
- `apps/cleansia-partner.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` → `api.email.sending_failed`
  (e.g. en: "Email sending failed")

No code references it (the backend error key it mirrored is gone). The sibling keys under `api.email`
(`invalid_format`, `invalid_email`) are still valid and must STAY. Tiny mechanical cleanup; no behavior.

**No-decision note:** pure consistency cleanup of orphaned i18n keys with zero consumers — no new
behavior or decision → skips the deliberation panel.

## Acceptance criteria
- [ ] **AC1** — `api.email.sending_failed` is removed from all 5 admin locale files and all 5 partner
  locale files (10 entries). The surrounding `api.email` group and its still-used siblings
  (`invalid_format`, `invalid_email`) are unchanged; every other key untouched.
- [ ] **AC2 (no orphan / no consumer)** — A repo-wide search confirms `sending_failed` (and any
  `api.email.sending_failed` lookup) has zero remaining references in TS/HTML; the i18n key-parity
  check across the 5 locales per app stays balanced after the removal.
- [ ] **AC3** — The two touched apps lint + build clean; the i18n parity guard reports no missing/extra
  key for the edited files.

## Out of scope
- The valid `api.email.invalid_format` / `api.email.invalid_email` keys (keep).
- The customer app (the key does not exist there — verify and do not add).
- Any backend change (T-0262 already removed the constant).

## Implementation notes
- 10 line deletions across `apps/{cleansia-admin.app,cleansia-partner.app}/src/assets/i18n/*.json`.
- Preserve JSON validity (no dangling commas) — the key sits between `email`-group siblings, so remove
  the line and fix trailing-comma if `sending_failed` was the last entry in any locale variant.
- Frontend-only; no backend, no nswag-regen, no ef-migration.

## Status log
- 2026-06-15 — **ready** (created by pm at Wave-6 close-out). Dep T-0262✓ (`done`, Wave-6). Residual the
  T-0262 `errors.*` scope did not cover (the `api.*` namespace). No owner decision, S, frontend-only →
  ready immediately. Wave-7 candidate (next-wave hygiene). Reviewer-per-developer; QA = key-parity +
  lint/build green. No panel (no-decision mechanical cleanup).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

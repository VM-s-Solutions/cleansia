---
id: T-0486
title: Android — wire the recurring-booking edit path (the repository function exists and has no caller)
status: draft
size: M
owner: android
created: 2026-08-02
updated: 2026-08-02
depends_on: [T-0485]
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Owner remark #6 (2026-08-02):** *"Cannot edit a recurring cleaning setup on either mobile app."*
Android half. **iOS is T-0487.** Both are rewritten from **T-0485**'s story before dispatch — the
scope below is what the PM verified exists, **not** the scope of the fix.

### Ground truth — PM-verified on `master` at `0e4ede1b`

The Android plumbing is **built and dead**:

| Layer | State |
|---|---|
| `core/recurring/RecurringBookingApi.kt:49` | `suspend fun update(body: UpdateRecurringBookingRequest): Response<RecurringBookingTemplateDto>` — **written**, maps to the generated `UpdateRecurringBookingCommand` at `:51` |
| `core/recurring/RecurringBookingRepository.kt:70` | `suspend fun update(request: UpdateRecurringBookingRequest): ApiResult<RecurringBookingTemplateDto>` — **written** |
| `core/recurring/RecurringBookingDtos.kt:69` | `data class UpdateRecurringBookingRequest` — **written** |
| callers in `features/recurring/` | **ZERO** — `grep '\.update('` across the feature package returns nothing |
| navigation | only `Routes.CreateRecurringBooking(orderId: String? = null)` (`navigation/Routes.kt:102`), navigated from four sites — all **create**, none **edit** |
| `RecurringBookingsScreen.kt:66` | comment: *"Create + edit ship via `CreateRecurringScreen` — entry points are the …"* — **the comment describes an edit path that does not exist** |

**That last row is the one to flag to a reviewer:** a comment asserting a shipped capability is worse
than silence, because the next developer trusts it. Repairing or deleting it is part of this ticket
regardless of what else lands.

## Acceptance criteria

> **⚠️ These AC are PROVISIONAL. T-0485's story replaces AC1–AC4 before this ticket goes `ready`.**
> They are written so the ticket is not empty and so the story author can see what the platform
> constrains. AC5–AC8 are stable.

- [ ] **AC1 (provisional) — an edit entry point exists** at the location T-0485 AC4 specifies, and a
      customer can change the fields T-0485 AC1 marks editable and see the change persisted after a
      cold restart. Evidence: a screen recording or before/after screenshots plus the reload.
- [ ] **AC2 (provisional) — the already-generated-orders behaviour matches T-0485 AC2** and the UI
      **tells the customer** which of their existing bookings this does and does not touch, in the
      same vocabulary the delete dialog already uses (`recurring_bookings_delete_dialog_what_stops` /
      `_what_stays`). Evidence: the copy plus the screenshot.
- [ ] **AC3 (provisional) — the dead plumbing is either USED or DELETED.** If T-0485's shape does not
      fit `RecurringBookingRepository.kt:70`'s signature, the unused function, its API sibling and
      its request DTO are **removed**, not left beside a second one. Evidence: `git diff --stat`.
- [ ] **AC4 (provisional) — the ViewModel carries an edit mode, not a copy of the create wizard.**
      `CreateRecurringViewModel.kt` is a create-only state machine (PM-read: no `templateId`, no
      `isEdit`, no load-existing). Whether it grows a mode or an edit VM is written beside it is
      stated with a reason. Evidence: the stated choice.
- [ ] **AC5 — the misleading comment at `RecurringBookingsScreen.kt:66` is repaired.** Whatever ships,
      that line either becomes true or goes. Evidence: the diff.
- [ ] **AC6 — the failure path is real.** A failed update surfaces the backend error through the
      existing snackbar/error contract, not a silent no-op. **This is the exact class of defect the
      partner-onboarding investigation found** (a validated value discarded behind a success toast —
      T-0507). Evidence: an error-path test plus the screenshot.
- [ ] **AC7 — a test that goes red against the current code (Gate 0.5 leg 1).** A ViewModel test
      driving the edit path, proved to fail before the wiring exists. Evidence: the red run, then
      green.
- [ ] **AC8 (Gate 0.5)** — `:customer-app` compile + `testDebugUnitTest` **un-cached**
      (`--rerun-tasks --no-build-cache`), task outcomes recorded.

## Out of scope

- **iOS** — T-0487.
- **Any backend change.** If T-0485 AC3 finds the command does not carry a needed field, that is a
  **backend ticket the story names**, and this ticket **holds** — it does not invent a contract.
- **The 3-step-wizard vs single-page-form divergence** — T-0481.
- **Catalog-name localization in the wizard** — **T-0477**, which edits
  `CreateRecurringScreen.kt:977/980/998`. **⚠️ Same file. Serialize: T-0477 first** (it is `S` and
  mechanical), then this. Recorded on both tickets.
- **The Plus gate's enforcement** — **T-0494**.

## Implementation notes

**No panel of its own — T-0485 is the panel.** This ticket implements a finalized story.

**Shared-file lane:** `features/recurring/CreateRecurringScreen.kt` is claimed by **T-0477**. One
writer at a time; **T-0477 goes first.**

## Status log
- 2026-08-02 — **draft (created by pm from the owner's remark #6).** The dead `update()` plumbing at
  three layers, the zero feature-layer callers, the create-only navigation route and the false comment
  at `RecurringBookingsScreen.kt:66` are all PM-verified at `0e4ede1b`. **`depends_on: [T-0485]`** —
  AC1–AC4 are explicitly provisional and get rewritten from the story; this ticket must not be
  dispatched against them.

## Review

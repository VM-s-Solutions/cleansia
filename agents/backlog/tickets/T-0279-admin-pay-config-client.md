---
id: T-0279
title: Replace admin-pay-config.service hand-rolled HttpClient with the generated AdminPayConfigClient
status: blocked
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
manual_steps: [nswag-regen]
sprint: 9
---

> **No-decision note (panel skipped):** mechanical swap of a hand-rolled HTTP service for the existing
> generated client; pattern-alignment only, no new behavior.

## Context

Audit finding #11 (MED). `pay-config-management/.../admin-pay-config.service.ts:42-96` hand-rolls
`HttpClient` URLs **plus parallel DTO interfaces** (`:7-38`) where a generated client already exists —
`core/admin-services/.../admin-client.ts:9131-9175` (`AdminPayConfigClient`) with the generated
command/response DTOs. It is consumed by `pay-config-management.facade.ts:17` and
`pay-config-form.facade.ts:46`.

**Why BLOCKED (not ready):** the audit explicitly notes this must **sequence after the pending IMP-3
nswag regen**. IMP-3 (per-employee pay config — `EmployeeId` on `EmployeePayConfig`, remaining: migration
+ backend commands + admin UI) is in-flight per `CLAUDE.md` and its **admin nswag-regen has not happened**.
Adopting `AdminPayConfigClient` before that regen risks consuming a client surface that IMP-3 is about to
reshape, forcing rework. This ticket **unblocks to `ready` the moment the owner confirms the IMP-3 admin
nswag-regen** (or confirms IMP-3 will not touch the pay-config client surface).

## Acceptance criteria

- [ ] **AC1 — Generated client in both facades.** `pay-config-management.facade.ts` and
  `pay-config-form.facade.ts` use `inject(AdminClient).adminPayConfigClient` for all pay-config reads/
  writes; the hand-rolled `admin-pay-config.service.ts` URL methods are removed.
- [ ] **AC2 — Hand-declared DTOs deleted.** The parallel DTO interfaces (`:7-38`) are deleted in favour
  of the generated command/response DTOs. No `any`.
- [ ] **AC3 — Service de-registered.** `admin-pay-config.service.ts` is removed from providers (and the
  file deleted if nothing else references it).
- [ ] **AC4 — Behavior identical, characterization-pinned.** The two facades' existing/added unit tests
  pin pay-config list + form save behavior and pass **unchanged** (same requests, same state).
- [ ] **AC5 — Mechanical checks green.** Admin app `nx build` (production) + `nx affected -t test` pass
  on a tree whose admin client reflects the **post-IMP-3** regen; `check-consistency.mjs` no new violation.

## Out of scope
- **No IMP-3 feature work** — this only adopts the generated client; the per-employee-override build is IMP-3.
- **No backend change** — `AdminPayConfigClient` already exists; this is FE-only adoption.
- **No new pay-config UI** beyond removing the hand-rolled service.

## Implementation notes

**Hold until the IMP-3 admin nswag-regen is confirmed by the owner** (the PM releases this ticket then).
If the owner confirms IMP-3 will NOT alter the pay-config client surface, it can be released early. Once
released: **single frontend dev + one reviewer**, serial (two facades + one service file).

**Routing:** `[frontend]`. `reviewer`. `qa` = Jest green + AC4 request-parity. No `security`, no `optimizer`.

## Status log
- 2026-06-22 — created by pm as **blocked**. Finding #11 VERIFIED (`admin-pay-config.service.ts` exists;
  `AdminPayConfigClient` exists in `admin-client.ts`). **NOT in any runnable Wave-8 batch** — blocked on
  the pending IMP-3 admin nswag-regen (an owner step that has not happened). Unblocks to `ready` on owner
  confirmation. No-decision (client adoption). `manual_steps: [nswag-regen]` (the IMP-3 regen this rides).
  Sized **S**.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->

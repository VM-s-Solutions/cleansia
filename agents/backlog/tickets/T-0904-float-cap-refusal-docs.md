---
id: T-0904
title: Document the cash float cap refusal and how it lifts
size: S
owner: Michael Chaban
created: 2026-10-31
updated: 2026-10-31
depends_on: []
blocks: []
stories: []
adrs: []
layers: [docs]
security_touching: false
---

## Context

Part of "Also fix the findings" (Mike, 31 October 2026). A cleaner holding more of the company's cash than
the float cap is not shown cash jobs and a take is refused; the docs did not say so, or how it lifts.
Ground truth: `/Users/michael/cleansia-work.noindex/partner-orders-build-20261031/gt/float.md`.

## Acceptance criteria

- [x] **AC1** — Business rules and the partner order-management page state the cap (`cash.float_cap`, none
  by default), that cash jobs are hidden and a take refused with `order.cash_float_cap_exceeded`, that card
  jobs are never held back, and that the jobs return once the cash is handed over or set off.
- [x] **AC2** — The Partner apps' Available tab states it when cash jobs are hidden (built in T-0902).

## Out of scope

Making the new-jobs digest, seat-open push, browse gate and contract preview respect the cap; seeding the cap
on DEV; the admin hint wording.

## Found, not fixed

- Four surfaces ignore the cap (new-jobs digest, seat-open push, browse gate, contract preview); a
  preferred hold can keep a cash job from everyone for up to 12 h; the cap is dormant on DEV (no seed).

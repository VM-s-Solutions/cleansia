---
id: T-0277
title: Hoist partner-app order date/time/money formatters onto :core (delete the divergent duplicate)
status: ready
size: S
owner: —
created: 2026-06-22
updated: 2026-06-22
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 9
---

> **No-decision note (panel skipped):** mechanical de-duplication onto the existing `:core` shared home
> (`cz.cleansia.core.format`), already consumed by customer-app. No new behavior — one canonical format
> is chosen and both apps render it identically.

## Context

Audit finding #2 (HIGH). `partner-app/.../features/orders/OrderDetailFormat.kt` re-implements order
date/time/money formatters (`:17/29/41`) that already exist in `:core`
(`core/.../format/OrderFormatters.kt:38/53/82`, already used by customer-app) — **and they DIVERGE**
from the customer-app rendering. The partner duplicate is consumed by 6 call sites (StatusTimeline,
PaymentCard, OrderTimerCard, ScopeCard, OrderMetadataRow per the audit). Two apps render the same order
the same data differently because of the fork.

## Acceptance criteria

- [ ] **AC1 — Characterization first.** Before deleting, a small unit test pins the **chosen canonical**
  output for the date, time, and money formatters (the `:core` format is canonical; if a partner-only
  shape is genuinely needed — e.g. `formatOrderTime` — it is **added to `:core`**, not kept in
  partner-app). The test documents the one format both apps now use.
- [ ] **AC2 — Duplicate deleted, call sites re-pointed.** `partner-app .../OrderDetailFormat.kt` is
  deleted; the 6 partner call sites import `cz.cleansia.core.format.*`. No partner-local formatter
  remains for these three concerns.
- [ ] **AC3 — One canonical format, no per-app drift.** Partner and customer apps render order
  date/time/money **identically** for the same input (the divergence is resolved by adopting the `:core`
  format; any partner-specific need is met by a new `:core` function, not a fork).
- [ ] **AC4 — Mechanical checks green + encoding-clean.** `:core` + `partner-app` + `customer-app`
  `compileDebugKotlin` + `testDebugUnitTest` pass; the diff is **byte-clean ASCII/UTF-8** (no BOM/
  mojibake — the past mass-edit hazard); `check-consistency.mjs mobile` no new violation.

## Out of scope
- **No change to customer-app rendering** beyond what already uses `:core` (it is the canonical baseline).
- **No new formatting concerns** beyond the three the partner duplicate covered.
- **The push-token cluster hoist** is T-0278 — separate subsystem, separate ticket.

## Implementation notes

`:core` is the ratified home for shared cross-app Kotlin (ADR-0011 era; `DeviceIdProvider`, `ApiResult`,
`OrderFormatters` already there). If `formatOrderTime` (or any used-by-partner function) is missing from
`:core`, **add it to `:core`** then point both apps at it. **Single android dev + one reviewer.**

**Serialization:** this ticket and **T-0278 both edit `:core`** — do **not** run them concurrently on
the `:core` module; serialize (T-0277 then T-0278, or vice-versa). Within each, partner-app and
customer-app edits are disjoint.

**Routing:** `[android]`. `reviewer`. `qa` = compile + JVM unit tests green + AC3 render-parity evidence.
No `security`, no `optimizer`.

## Status log
- 2026-06-22 — draft → ready (created by pm). Finding #2 (traced file:line evidence in the audit:
  `OrderDetailFormat.kt:17/29/41` vs `OrderFormatters.kt:38/53/82`). No-decision (dedup onto `:core`).
  `manual_steps: []`. Sized **S**. **Serialize with T-0278 on `:core`.**

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->

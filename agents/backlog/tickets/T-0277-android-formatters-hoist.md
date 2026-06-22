---
id: T-0277
title: Hoist partner-app order date/time/money formatters onto :core (delete the divergent duplicate)
status: done
size: S
owner: android
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
- 2026-06-22 — ready → review (android). Dedup complete, behavior-preserving.

  **What changed:**
  - `:core` `OrderFormatters.kt` — added `formatOrderTime(iso, locale)` (the one helper customer
    lacked); follows the existing `:core` idiom (custom `HH:mm` pattern, non-null, `"—"` for
    null/blank, raw input echoed on parse failure). `formatOrderDateTime` / `formatOrderPrice` were
    already canonical.
  - **AC1 characterization-first:** new `core/.../format/OrderFormattersTest.kt` (8 tests, pure JVM)
    pins the one canonical output under fixed UTC + `Locale.ENGLISH`: date `"Apr 22 · 10:00"`,
    time `"10:00"`, price `"1,200 Kč"` / `"1,200 €"` / `"$1,200"`, CZK default for blank currency,
    `"—"` for null/blank, raw echo on unparseable. Written and confirmed green before re-pointing.
  - **AC2:** partner `OrderDetailFormat.kt` — removed the three divergent formatters
    (`formatOrderDateTime` localized `MEDIUM/SHORT`, `formatOrderTime` `SHORT/SHORT`,
    `formatOrderMoney` `"1200 CZK"`). Kept the out-of-scope, `:core`-absent helpers in the same file
    (`OrderAddress.formatSingleLine`, `emojiForExtraSlug`, `nameForExtraSlug`) untouched.
  - **AC2 call sites (6):** `StatusTimeline`, `PaymentCard`, `OrderTimerCard`, `ScopeCard`,
    `OrderMetadataRow` now import `cz.cleansia.core.format.*`. `formatOrderMoney` → `formatOrderPrice`;
    redundant `?: "—"` elvis dropped where the `:core` helper is already non-null (it returns `"—"`
    for null/blank — same observable result, incl. `toIso(null)` paths). `OrderMetadataRow` keeps its
    hide-the-date-row-when-blank behavior via a `takeIf { isNotBlank() }` guard on the raw input.
  - **AC3 render-parity:** partner + customer now call the identical `:core` functions; order
    date/time/money render byte-identically for the same input. The intentional divergence (partner
    previously rendered locale-`MEDIUM` dates and `"123 CZK"` money) is resolved by adopting the
    `:core` form (`"Apr 22 · 10:00"`, `"1,200 Kč"`) — confirmed correct for both apps.

  **Test evidence (AC4):** `./gradlew.bat --offline` —
  `:core:testDebugUnitTest` green (`OrderFormattersTest` 8/8, full suite incl. existing 13);
  `:partner-app:compileDebugKotlin` + `:partner-app:testDebugUnitTest` green (26);
  `:customer-app:compileDebugKotlin` + `:customer-app:testDebugUnitTest` green (222 — additive
  `:core` change, no customer source touched). `check-consistency.mjs mobile` → 27 violations, **all
  pre-existing in untouched files (E1 flag-bag VMs, E6 collectAsState in NavHost, etc.); zero in any
  file this ticket touched — no new violation.** Edited files verified no-BOM, valid UTF-8, CRLF
  preserved (incl. the emoji literals in `OrderDetailFormat.kt`).

  **Deviations:** none. **Manual steps:** none (no DTO/response shape change — pure UI formatting).

  **Parity note for iOS (T-0277 mirror):** the shared order formatters now live in one home and
  render one way on both apps: date-time `"MMM d · HH:mm"`, time-only `"HH:mm"`, price = locale
  grouping + native symbol (`CZK→Kč`, `EUR→€`, `USD→$`, `GBP→£`, else `"<n> <code>"`), default
  currency CZK, `"—"` for null/blank, raw echo on parse failure. iOS must collapse any partner-side
  formatter fork onto the same single form.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->

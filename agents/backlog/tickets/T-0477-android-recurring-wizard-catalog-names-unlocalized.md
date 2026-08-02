---
id: T-0477
title: Android recurring wizard renders service and package names in the catalog's raw language
status: draft
size: S
owner: android
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Owner remark #2 (2026-08-02):** *"No translations for the recurring-booking setup, both mobile
apps."* This ticket is the **Android half, and it is a confirmed defect with a located cause.** The
iOS half is **T-0478** — different cause, different evidence, do not merge them.

### Ground truth — PM-verified on `master` at `0e4ede1b`

**What is NOT wrong, stated first so nobody re-derives it:**

| Check | Result |
|---|---|
| `recurring_*` keys in `customer-app/src/main/res/values/strings.xml` | **62** |
| the same keys in `values-cs`, `values-sk`, `values-uk`, `values-ru` | **62 in every one** |
| are the translations real, or English copies? | **Real.** `recurring_bookings_empty_subtitle` is genuinely rendered in cs/sk/uk/ru (PM read all five values) |
| hardcoded user-visible literals in `CreateRecurringScreen.kt` | **none found** — the only string literals are a Compose animation label and a `"• "` bullet |

**The actual defect is the catalog text, not the chrome:**

```
CreateRecurringScreen.kt:980   title = pkg.name.orEmpty(),
CreateRecurringScreen.kt:998   title = svc.name.orEmpty(),
```

Both take the **raw backend `name`** and never consult the per-language `translations` map that rides
on the same DTO. Every other catalog surface in the platform localizes client-side. **iOS gets this
right on the very same screen** — `CreateRecurringScreen.swift:167` and `:180` call
`package.localizedName(for: locale)` / `service.localizedName(for: locale)`, backed by
`Features/Booking/Catalog/CatalogLocalization.swift:7-9`
(`translations[languageCode]?.name ?? fallback`).

So a Czech user setting up a recurring clean sees the wizard chrome in Czech and **"Deep cleaning",
"Standard package"** in English, in the middle of it. That is exactly the report.

**`CreateRecurringScreen.kt:977` already reads `it.name`** for the package "Includes:" service list —
same bug, one line up, same fix.

## Acceptance criteria

- [ ] **AC1 — the three call sites localize.** Given a catalog whose services/packages carry a
      `translations` entry for the active app language, When the recurring wizard renders the
      "what" step, Then package titles (`:980`), service titles (`:998`) **and** the package
      "Includes:" list (`:977`) show the translated name. Evidence: the diff plus a `cs` or `ru`
      screenshot of the step.
- [ ] **AC2 — the fallback chain is the same one the rest of the app uses, and is named.** When no
      translation exists for the active language, Then the raw `name` is shown (never a blank, never
      the key). The verdict names the existing Android helper reused — **or**, if none exists, states
      that plainly and says why a new one is correct rather than a fourth copy of the same three
      lines. Evidence: the named helper at file:line.
- [ ] **AC3 — the language source is the app's, not the device's.** The verdict states which language
      value drives the lookup and cites it. If the customer app has a per-app language preference
      that differs from the system locale, the wizard must follow **the app's**. Evidence: the
      resolution path traced at file:line.
- [ ] **AC4 — a test that goes red against the current code (Gate 0.5 leg 1).** A unit test over the
      title-resolution function with a two-language fixture, proved to **fail** against
      `pkg.name.orEmpty()`. Evidence: the red run pasted, then green.
- [ ] **AC5 — the sweep, bounded.** Grep the customer app for other `\.name.orEmpty()` /
      `\.name ?: ""` catalog renders. **Do not fix them here** — list them in `## Review` with
      file:line so a follow-up ticket can be filed with a real scope. Evidence: the list, or "none".
- [ ] **AC6 (Gate 0.5)** — `:customer-app` compile + `testDebugUnitTest` **un-cached**
      (`--rerun-tasks --no-build-cache`), task outcomes recorded.

## Out of scope

- **iOS** — T-0478. iOS is the *correct* implementation here and is the reference.
- **Adding new `recurring_*` string keys.** All 62 exist in all 5 locales; this ticket adds none.
- **The 19-key / screen-shape divergence between the two recurring wizards** — see `## Implementation
  notes`; it belongs to **T-0481**.
- **Backend-side translation of the catalog.** The platform localizes catalog names client-side from
  a `translations` map. Changing that is an architecture decision, not this ticket.

## Implementation notes

**No panel — one-line "no-decision" note:** this applies an existing, shipped mechanism
(client-side `translations[languageCode]` lookup, already used by iOS on this exact screen and by
the Android booking flow) to three call sites that skipped it. No new behaviour, no new decision.

**A finding recorded here rather than acted on, because it is a different ticket's work:** the two
recurring wizards are **not the same screen**. Android's is a **3-step wizard** (`step_what` /
`step_when` / `step_where_pay`, frequency cards with sublines and a "Most popular" badge, morning /
afternoon / evening time periods) at ~1071 lines; iOS's is a **single-page form** at 268 lines
(`FrequencySection`, `TimeSection`, `AddressSection`, `ServicesSection`, `PaymentSection`,
`StartsSection`). That is why Android has **19 `recurring_*` keys iOS does not** and iOS has 3
(`recurring_plus_gate_*`) Android does not. **PM-measured, not estimated.** It is a real parity gap
and it is routed to **T-0481**, the parity audit — not smuggled into a localization fix.

## Status log
- 2026-08-02 — **draft (created by pm from the owner's remark #2).** The five-locale key coverage, the
  translated values, the absence of hardcoded literals, the three unlocalized call sites and the iOS
  reference implementation were all PM-verified at `0e4ede1b`. **The report ("no translations") is
  materially wrong about the cause and right about the symptom** — the chrome is fully translated;
  the catalog text is not.

## Review

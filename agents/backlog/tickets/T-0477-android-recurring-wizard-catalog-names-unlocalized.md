---
id: T-0477
title: Android recurring wizard renders service and package names in the catalog's raw language
status: done
size: S
owner: android
created: 2026-08-02
updated: 2026-08-05
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
- 2026-08-05 — **the three call sites were ALREADY FIXED in `2012b014`** (the owner's-remark-list PR),
  which never updated this ticket. AC1/AC2/AC3 verified as shipped; **AC4 was the real gap** — there
  was no test over the resolution at all. Closed here.

## Review — android (2026-08-05)

**Gate 0: the code fix already shipped; the test did not.** `CreateRecurringScreen.kt:990,994,1011`
already read `localizedName(...)`, landed by `2012b014`. The screen also moved package —
`features/recurring/`, not `features/booking/` as the ticket's paths say. What was missing was AC4:
no test anywhere referenced the resolution, so the fix was one careless edit from silently reverting.

**AC1/AC2 — the helper, named.** `localizedName` / `localizedDescription`
(`customer-app/.../features/booking/ServicesStep.kt:133-143`) is the app's one way, with **24 call
sites** across booking, home, orders, order-detail and this wizard. No fourth copy was written.

**AC3 — the language source is the APP's, and here is the chain.** `AppLocale.kt:45,66` (`:core`)
calls `AppCompatDelegate.setApplicationLocales`, which rewrites the Configuration; the helper reads
`LocalConfiguration.current.locales.get(0)?.language`. `AppSettingsRepository.kt:47-52` records the
same reasoning for the email-language path — read the configuration, not
`AppCompatDelegate.getApplicationLocales()`, so there is one source of truth right after a switch.

**AC4 — the test, and the one change to production code.** The pure part of the resolver is now
`pickTranslatedName` / `pickTranslatedDescription` (same file, `:111-127`), taking `lang` as a
parameter; the two `@Composable`s are one-line delegates. That is the only production edit in this
ticket, and it exists so the fallback chain is assertable without a composition — the catalog's own
"hoist the decision into a value type the screen and a test can both name" rule.
`RecurringCatalogLocalizationTest` (4 tests) covers the two-language fixture, independent
name/description fallback, and a **call-shape** pin on the three wizard sites. The pin asserts whole
call expressions (`localizedName(pkg.translations, pkg.name)`) rather than the symbol, because a bare
`localizedName` substring is satisfied by the import line alone.

**AC5 — the sweep, bounded, NOT fixed.** No further catalog-name renders bypass the helper. The sweep
did surface a **different** defect class, recorded for a follow-up ticket: `CodeDto.name`
(`core/user/UserDto.kt:20-25`) is a server-supplied English enum name with **no** translations map
beside it, rendered raw at `features/disputes/DisputeDetailScreen.kt:428,433` and
`features/disputes/DisputesListScreen.kt:274,280`. Dispute reason and status therefore read English in
cs/sk/uk/ru. It is not fixable by this ticket's mechanism — there is nothing to look up — it needs an
ordinal→`stringResource` map, which is exactly the shape `patterns-mobile.md:551-561` prescribes and
which the order-status surfaces already use (`OrderDetailTimelineAndReview.kt:80` degrades to `.name`
only when its `labelRes` map misses, so it is the mild form of the same thing).
`core/servicearea/CustomerServiceAreaDataSource.kt:46,66` is a DTO mapping of city names, not a render
— refuted, not omitted.

**AC6 (Gate 0.5) — un-cached.** `:customer-app` compile + `testDebugUnitTest`, `--rerun-tasks
--no-build-cache`: **BUILD SUCCESSFUL, exit 0, 53 actionable tasks: 53 executed**, zero `FROM-CACHE`
(the only `UP-TO-DATE` lines are the actionless `pre*Build` anchors). **508 tests / 57 classes / 0
failures / 0 skipped**, read from the JUnit XML — the 503/55 baseline plus this ticket's 4 tests and
the string ticket's 1. Two named mutations, each reddening exactly one test:
`translations[lang]?.name ?: fallback` → `fallback` reddens *the active language wins over the
catalog's own language*; reverting the package call site to `pkg.name` reddens *the wizard's what-step
renders every catalog name through the resolver*. Both restored byte-exact by md5
(`c21a94e9b197da733510c9afacb307d4` ServicesStep.kt, `3b2f00722163f35702789343fd8adb99`
CreateRecurringScreen.kt — identical before and after; `git status` shows CreateRecurringScreen.kt
unmodified). `check-consistency.mjs` → **22 violations = the master baseline of 22**, none in a
touched file. Every touched file `utf-8`, no BOM.

**Not verified:** the AC1 `cs`/`ru` screenshot. There is no emulator or device in this environment and
nothing rendered was observed.

---
id: T-0375
title: "Android customer BUG — home_hero_greeting bakes the hardcoded name \", Michael\" into EVERY user's greeting (strings.xml:116, ×5 locales)"
status: done
size: S
owner: android
created: 2026-07-03
updated: 2026-07-19
depends_on: []
blocks: []
stories: []
adrs: [ADR-0018]
layers: [android]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: phase/ios-fix1 T-0373 Slice-F Gate-DP divergence (1) — Android finding raised during the iOS port, deliberately NOT ported
---

> **Found by the iOS parity port (T-0373 / phase/ios-fix1 Slice F), present on the LIVE Android customer
> app.** While porting the Home upsell carousel, Gate-DP's line-by-line comparison surfaced that Android's
> hero greeting is the owner's own first name baked into the string resources — every user of the customer
> app is greeted "Welcome back, Michael". iOS deliberately did NOT port it (divergence (1) recorded in
> T-0373) and shipped the name-less "Welcome back" set.

## Context
`customer-app/src/main/res/values/strings.xml:116`:
`<string name="home_hero_greeting">Welcome back, Michael</string>` — and the same hardcoded ", Michael"
tail in ALL FIVE locales (`values-cs` "Vítejte zpět, Michael", `values-sk` "Vitajte späť, Michael",
`values-uk` "З поверненням, Michael", `values-ru` "С возвращением, Michael"; each at line 116). Consumed
by the hero slide at `HomeTab.kt:489` (`topRes = R.string.home_hero_greeting`). This is leftover
dev-fixture copy, not a template: the string has no format placeholder and the call site passes no
argument.

## Acceptance criteria
- [ ] **AC1 (no baked name)** — Given any signed-in user, When the Home hero slide renders, Then the
  greeting contains NO hardcoded personal name in ANY of the 5 locales. Fix is EITHER (a) the name-less
  set (exactly the iOS T-0373 values — cross-platform copy parity, the cheap option) OR (b) a real
  `%1$s` placeholder filled from the user's profile first name with a name-less fallback when the profile
  has none. Pick ONE and record it; if (b), iOS gets a parity follow-up note (do not silently diverge the
  two platforms' copy).
- [ ] **AC2 (locale sweep)** — All 5 `values*/strings.xml` updated consistently; no other `home_hero_*` /
  `home_upsell_*` string carries a baked name (grep the resource tree for ", Michael" / "Michael" —
  zero user-facing hits).
- [ ] **AC3 (non-regression)** — `:customer-app` compiles; existing home/UI tests green; no layout break
  on the hero slide with the longest locale string.

## Out of scope
- The iOS greeting (already name-less by T-0373; only revisit under AC1 option (b) as a follow-up note).
- Any other hardcoded-copy sweep beyond the `home_hero_*`/`home_upsell_*` family (AC2's grep is a guard,
  not a sweep mandate).

## Implementation notes
- No-decision note: a string-resource bug fix composing the existing Home surface — no new behavior
  decision, panel skipped. The only choice (name-less vs placeholder) is recorded in AC1 and stays
  copy-level.
- If option (b): the profile first name is already available to `HomeTabViewModel`'s data sources; keep
  the fallback name-less, never an empty ", ".

## Status log
- 2026-07-03 — filed `proposed` by pm at the phase/ios-fix1 close, from T-0373's Gate-DP divergence (1)
  (the iOS port refused to replicate the bug; the Android fix was raised as this follow-up). Medium
  priority: user-visible on every Android customer install, trivial fix. Not dispatched.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
- 2026-07-19 — **frontmatter reconciled (proposed → done)** — fixed in `13431f2b` (PR #123): AC1 option (a) name-less iOS-parity copy in all 5 locales; AC2 zero 'Michael' hits repo-wide; re-verified 2026-07-19 with full suites green (244+122).

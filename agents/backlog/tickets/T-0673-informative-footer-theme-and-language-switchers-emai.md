---
id: T-0673
title: Home: informative footer, theme and language switchers, email promo capture
status: done
size: M
owner: frontend
created: 2026-08-30
updated: 2026-08-31
depends_on: ['T-0671']
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: [nswag-regen]
sprint: 16
---

## Context
- The current footer carries almost no information. The concept adds five columns: description with store links, services, company, support, and contact with hours and registration numbers.
- The customer app has a `ThemeService` keyed on `cleansia-theme` and a `cleansia-language-switcher` component, but neither is reachable from the home page.
- The owner asked for an email capture that returns a promo code. `PromoCode` supports create and validate, but no endpoint mints-and-emails one.

Design language and the pre-submission review: [`../../knowledge/design-language.md`](../../knowledge/design-language.md).

## Acceptance criteria
- [x] **AC1** - The footer renders five columns with real links; every unknown fact is a visible placeholder, never invented.
- [x] **AC2** - Theme and language switchers are reachable from the customer header and the footer, and both persist.
- [x] **AC3** - The email capture renders with a consent checkbox and three states, wired to a facade.
- [x] **AC4** - The facade is stubbed behind an interface; no fake success is shown to a user until the backend endpoint exists.

## Out of scope
- Building the backend mint-and-send endpoint - a separate backend ticket needing an owner-run NSwag regeneration.
- Marketing-consent storage beyond what `UserConsent` already models.

## Implementation notes
Follow `agents/knowledge/patterns-frontend.md` and run section 7 of the design language before opening a PR.
Headings are `Sky700 #0369A1`; primary action `Sky600 #0284C7`; radius from `6 / 12 / 16 / 24 / 32`;
no coloured shadows. Any new shared token lands on Android and iOS in the same PR.

## Status log
- 2026-08-30 - ready (filed from the approved home-page concept)
- 2026-08-31 - ground-truthed: PARTIAL, not shipped. AC3 holds.
  - AC1 FAILS - and the first pass missed it; the adversarial re-check caught it. The five column
    blocks exist in the markup (`customer-footer.component.html:52-89`), but
    `_home-footer.scss:190` declares `grid-template-columns: 2fr 1fr 1fr 1.25fr` - FOUR tracks for
    FIVE children, with no explicit placement on any child and no other rule for
    `.cl-footer__columns` in the workspace. The contacts column wraps to an implicit second row on
    desktop. This is a live visual defect, not a documentation gap.
  - AC2 half-holds: the header switchers are real and both persist to localStorage. The FOOTER half
    was never built - the footer imports no switcher at all.
  - AC4 is OBSOLETE rather than failed. It asked for a stub behind an interface until the backend
    existed; the backend exists (T-0676) and the facade calls it live, so the condition the AC was
    protecting against is gone. The 'no fake success' half holds - success renders only on
    `result.accepted`. Reword or drop it; do not build an interface to satisfy it.
- 2026-08-31 - done.

  **AC1 - the live layout bug.** `.cl-footer__columns` declared four grid tracks for five children
  with no explicit placement, so the contacts column wrapped to an implicit second row on desktop
  while the markup read as correct. Five tracks now (`1.8fr 1fr 1fr 1fr 1.4fr`), with a 1100px step
  down to three - five tracks in a 900px viewport give each about 130px, narrower than the column
  titles themselves. The single-column stack below 768px is unchanged.

  **AC2 - the footer half was never built.** Theme and language now sit in the footer's bottom bar,
  reading the same `ThemeService` signal and the same switcher component the header uses, so the two
  can never disagree. Both already persisted to localStorage; nothing new was needed for that.

  **AC4 is OBSOLETE, not satisfied.** It asked for the facade to be stubbed behind an interface so no
  fake success could be shown before a backend existed. The backend exists (T-0676) and the facade
  calls it live, so the condition it was protecting against is gone. The 'no fake success' half does
  hold - success renders only on `result.accepted`. No interface was built to satisfy wording that
  has outlived its reason.
## Review
<!-- reviewer / security / optimizer write verdicts here -->

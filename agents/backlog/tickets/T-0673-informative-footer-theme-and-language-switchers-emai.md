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
- 2026-08-31 - REOPENED. The footer is the largest single divergence on the page:
  - Background is white. The artboard's footer is `#0C4A6E` navy with `#BAE6FD` links.
  - Four columns, not the artboard's five (`1.5fr 1fr 1fr 1fr 1.1fr`).
  - No App Store / Google Play badges; the artboard has both under the brand blurb.
  - The language switcher floats at the top-right of the footer body instead of sitting in the
    bottom bar beside the theme control, as the artboard draws it.
  Navbar, also this ticket's territory: the sign-in link reads "Přihlásit se" (artboard: "Přihlásit"),
  the action reads "Objednat" (artboard: "Objednat úklid"), and there is a person icon the artboard
  does not have.
- 2026-08-31 - done, verified by screenshot. The footer is `#0C4A6E` navy with the artboard's five
  columns, the App Store and Google Play badges, and language plus theme as chips in the bottom bar.
  The brand is a white wordmark: the logo is a blue raster and cannot be recoloured for a navy
  ground, which is also why the artboard sets it as type.
  Navbar: "Přihlásit", "Objednat úklid", and the account control appears only once there is an
  account.

  REPORTED, not built: the artboard lists four destinations that have no page — About us, Become a
  cleaner, Careers and Cookie settings. They render at the right position in the right column, in a
  muted tone, rather than linking to a 404. Creating those pages is product work, not this ticket.

- 2026-08-31 - REOPENED again, then measured rather than eyeballed. A geometry
  extractor now dumps every element's box and computed styles from both the
  artboard render and the live page, and the two are diffed numerically. That
  found what looking could not:

  **`html { font-size: 14px }`.** Every token I had written in `rem` rendered at
  87.5 % of the artboard: a 58px display heading came out at 51, 38px at 33, 17px
  at 15. The whole page was one eighth too small, in every dimension that used a
  rem. All home tokens and partials are px now, because the artboard is px.

  **The content column was 1200, not 1312.** `$max-width` is 1200; the artboard
  is drawn on 1440 with 64px gutters. Every section was 112px narrower than the
  design, which is what read as "misaligned" — the grids were right, the frame
  was not. A `$home-width: 1312px` now carries it, kept separate so the wizard,
  catalogue, legal and tracking pages stay where they were designed.

  **`.cl-section` gutters were `2rem`** — 28px against the 14px root, not the
  artboard's 64.

  Also fixed: the navbar sat on white because the shell's 64px spacer no longer
  matched the 96px bar, so the hero could not run under it; the FAQ action was a
  full-width `cleansia-button` where the artboard has a compact pill; the Plus
  perks reflowed to 3+1 once the card reached its real width; the gallery had no
  top foam edge; `text-wrap: balance` was turning two-line card headings into
  three, so it is limited to the display sizes the artboard hand-breaks.

- 2026-08-31 - owner asks, done: the cookie row in the footer is a real control
  that reopens the consent panel on its settings tab (a `CookieConsentService`
  the banner watches, since the banner hides itself once a choice is stored);
  the FAQ contact and promo send actions are 40px rather than 56px. The hero,
  CTA, quote and Plus actions stay at the artboard's 56px — "a few buttons" was
  read as the ones sitting beside a field or inside a column, and the four that
  carry the page are the artboard's own size. Say if that reading is wrong.

  Cursor audit: every interactive element reports `pointer`. The two that do not
  are correct — a static price chip and a text input.

## Review
<!-- reviewer / security / optimizer write verdicts here -->

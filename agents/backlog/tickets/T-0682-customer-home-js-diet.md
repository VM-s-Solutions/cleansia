---
id: T-0682
title: Customer home page — the remaining Lighthouse gap is JavaScript, measured 72 → 94 with app JS removed
status: todo
size: M
owner: —
created: 2026-09-06
updated: 2026-09-06
depends_on: [T-0681]
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

With SSR working (see [T-0681](T-0681-production-never-serves-ssr.md) — without it none of this
matters), the customer home page scores **73** on the Lighthouse mobile preset. Desktop scores 97, so
this is entirely a mobile CPU/network problem.

Where the 27 points go, from the report's own weights:

| audit | weight | score | points lost |
|---|---|---|---|
| largest-contentful-paint | 25 | 0.26 | **18.5** |
| first-contentful-paint | 10 | 0.49 | 5.1 |
| total-blocking-time | 30 | 0.90 | 3.0 |
| speed-index | 10 | 0.94 | 0.6 |
| cumulative-layout-shift | 25 | 1.00 | 0.0 |

### The cause was isolated, not guessed

Doctored copies of the **real** 227 KB SSR document were served with one thing changed each, then run
through the identical Lighthouse preset:

| variant | score | FCP | LCP | TBT | CLS |
|---|---|---|---|---|---|
| A baseline (static, TTFB 10 ms) | 72 | 3.7 s | 4.6 s | 210 ms | 0.023 |
| B 157 KB of inline CSS stripped | **58** | 3.5 s | 4.6 s | 190 ms | 0.305 |
| H all four web fonts removed | 72 | 3.5 s | 4.4 s | 290 ms | 0.017 |
| I **all app JS removed** (upper bound) | **94** | 2.0 s | 2.7 s | 0 ms | 0.001 |

**The JavaScript owns essentially the whole remaining gap.** The CSS and the fonts own none of it.

### Payload, measured

802 KiB over 35 requests: script 407.5 KiB / 11 requests, image 141.9, font 94.1, stylesheet 86.7,
third-party 129.4 over 14 requests.

Largest assets (transfer / raw):

| | |
|---|---|
| 199.3 KiB / 859 KB | `chunk-…js` — **PrimeNG, 51.2% unused on this page** |
| 71.5 KiB / 210 KB | `@angular/core` + hydration (irreducible) |
| 51.4 KiB / 331 KB | `styles-….css` — 87.9% unused |
| 51.1 KiB / 223 KB | `main-….js` — app + Sentry + NgRx |
| 45.1 KiB | `mascot-mopping-480.webp` — **the LCP element** |
| 29.6 KiB / 365 KB | `primeflex.min.css` — **99.88% unused** |
| 29.0 KiB / 104 KB | `/assets/i18n/en.json` |
| 23.2 KiB / 224 KB | NSwag client chunk — 93.2% unused |

PrimeNG is in the **eager** graph because the hero's quick-quote widget imports DatePicker and Select
directly.

## Acceptance criteria

- [ ] **AC1** — Given each change below, When the same Lighthouse command is re-run, Then the score
      movement is recorded. **A change that does not move the number is reverted, not kept.**
- [ ] **AC2** — Given the PrimeNG deferral, Then the quick-quote widget still works on first
      interaction.

## Ranked, with the basis stated

1. **Defer PrimeNG off the landing page** — `quick-quote.component.ts` imports DatePicker and Select
   statically, which is what pulls 859 KB raw into the eager chunk. `@defer (on interaction)` on the
   `<p-datePicker>`/`<p-select>`. *Bytes measured; score gain **estimated** — the 72→94 upper bound
   says the headroom is in JS, but nobody measured this specific removal. Re-measure; if it does not
   move, stop.*
2. **Three dead providers in `app.config.ts`.** `BrowserAnimationsModule` contradicts
   `provideAnimationsAsync()` and wins by ordering — deleting it is a bug fix, not just perf.
   `StoreDevtoolsModule`'s `!environment.isDevelopment` ternary did not tree-shake. *Presence
   measured in the bundle; gain estimated, small.*
3. **i18n out of the critical path.** The server loader already holds the parsed object; put it in
   `TransferState` so the client does not re-fetch 104-156 KB serially after `main.js` parses.
   *Sizes measured; gain estimated.*
4. **The mascot asset** — `mascot-idea-480.webp` (47,338 B) → the tile variant (19,238 B), saving
   28,100 B. **Not a free swap**: the SCSS is `height: 156px; width: auto` and the assets are 480×480
   vs 340×280, so it would render ~189×156 rather than 156×156. Needs a visual check.
5. **Split the global stylesheet** — 303,853 of 399,926 source bytes belong to other routes. *Expect
   near-zero score movement; this is hygiene. Pair it with a real budget — the current 2.5 MB warning
   sits above the actual 1.93 MB and has never fired.*

## Out of scope — measured cargo cult

- **Web fonts.** Removing all four woff2 scored 72 → **72**. Worth nothing.
- **Shrinking the inline critical CSS.** Measured **negative**: 72 → 58, CLS 0.023 → 0.305. It is
  70.7% of the document and it is load-bearing.
- **SSR render time and micro-cache tuning.** Non-factor: 330 ms TTFB scored 73, 10 ms scored 72.
- **The 29 MB of unreferenced assets.** Nothing requests them — zero Lighthouse effect. Real, but an
  image-hygiene ticket.
- **`preconnect` to the CDN.** The honest fix is deleting PrimeFlex (99.88% unused, and the home
  page's ten templates use zero PrimeFlex classes), not hinting it. Its own ticket.
- **Touching the hero preload.** Breaking it moved LCP `resourceLoadDelay` from 14 ms to 527 ms.

## Status log

- 2026-09-06 — filed from a measured investigation. Every number above is observed on a production
  build; every estimate is labelled as one.

## Review

<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

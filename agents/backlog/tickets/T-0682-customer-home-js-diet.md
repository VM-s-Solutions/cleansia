---
id: T-0682
title: Customer home page — the remaining Lighthouse gap is JavaScript, measured 72 → 94 with app JS removed
status: done
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

- [x] **AC1** — Recorded below. Two of the five ranked items were **refuted by measurement and not
      built**; one was built against a cause this ticket had identified wrongly.
- [x] **AC2** — Void: the quick-quote deferral was never built, because removing those two controls
      outright saves **999 bytes** (see below). The widget is untouched.

## What was measured, and what it refuted

Two harnesses, because the score alone could not carry a verdict:

- **Eager JS on the landing page** — every script the page actually requests, summed. Deterministic,
  zero variance. This is the metric the verdicts below rest on.
- **Lighthouse mobile**, same preset as the baselines above. **It could not resolve a change on this
  machine.** `benchmarkIndex` ranged **712 to 3683 across runs** — a 5x CPU swing — against an
  effect worth a couple of points. An interleaved A/B/A/B (both servers up, one Chrome, alternating)
  put the entry-point fix at a **median +2 points, per-round deltas [2, -3, 3, 1]**: noise. The
  absolute scores here (39-40) also disagree with this ticket's own 73 baseline, so the harness, not
  the change, is what moved. Load time under Slow 4G + 4x CPU is reported instead.

| | eager JS (raw) | vs baseline |
|---|---|---|
| baseline | 1,743,385 | — |
| dead providers only | 1,725,076 | -18,309 |
| **+ removing both PrimeNG controls from quick-quote** | 1,724,077 | **-999 more** |
| + component entry points | 1,503,767 | -221,309 more |
| **shipped** | **1,503,904** | **-239,481 (-13.7%)** |

Page load, Slow 4G + 4x CPU throttling, median of 3, to `networkidle`:

| | baseline | shipped | |
|---|---|---|---|
| `en` | 9,511 ms | 8,383 ms | **-1,128 ms (-11.9%)** |
| `ru` | 10,275 ms | 8,653 ms | **-1,622 ms (-15.8%)** |

### Item 1's stated cause was wrong

This ticket said quick-quote's static `DatePicker`/`Select` imports are "what pulls 859 KB raw into
the eager chunk". Removing **both controls outright** — the ceiling for any deferral — moved
**999 bytes**. The chunk stayed at 877 KB.

The real cause is `@cleansia/components`: a barrel of `export * from './lib'` over 32 components
reaching **17 distinct PrimeNG modules**. Importing one name from it puts all 17 on the critical
path. The fingerprints were in the eager chunk all along — `MULTISELECT_INSTANCE`,
`FLOATLABEL_INSTANCE`, "Jump to Page Dropdown" — components the landing page never renders. Five
files reached the barrel: three in the shell, two in the home feature.

`"sideEffects": false` was tried first as the one-line fix and made **no difference at all**, so it
was reverted; the mechanism is chunk assignment, not side-effect retention.

### Item 4's asset is not on the critical path

`mascot-idea-480.webp` is `loading="lazy"` and never requested during load. The LCP element is
`mascot-mopping-480.webp` (45,894 B), a different asset this ticket does not mention. The swap is
still worth 28,100 B for readers who scroll — see the findings, it needs a visual call.

### Item 5 has nothing to fix

Lighthouse reports **no render-blocking resources**: the CDN stylesheets already load through a
`media="print"` swap. Splitting the global stylesheet was predicted near-zero by this ticket and the
measurement agrees, so it was not built.

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
- 2026-09-06 — **shipped three changes; two ranked items were refuted and left unbuilt.**
  1. **Component entry points** (`@cleansia/components/*` in `tsconfig.base.json`, five call sites) —
     -221 KB. This is the fix item 1 was reaching for, against the cause it named wrongly.
  2. **Dead providers** — `BrowserAnimationsModule` (which defeated `provideAnimationsAsync()` by
     ordering: a bug, not just weight), `StoreDevtoolsModule` and `withJsonpSupport()`. All three were
     confirmed present in the production bundle before removal. -18 KB.
  3. **i18n via TransferState** — the one change with a large, clearly attributable timing win. The
     fetch was 106,382 bytes starting at **909 ms**, and it could not begin until `main.js` parsed;
     because the `APP_INITIALIZER` awaits it, it blocked bootstrap and so hydration. Now zero
     requests. Verified in all five locales: TransferState present, no HTTP fetch, no raw keys, right
     `html lang`. It is a **trade**, not a free win — Cyrillic escapes to `\uXXXX` inside HTML and
     compresses worse, so brotli-over-the-wire is `en` -1,696 B but `cs`/`ru` **+16 KB**. It still
     wins on time in both, because one blocking round trip costs more than parallel early bytes.
  Verified: typecheck, lint, unit tests, and all three production builds green.

## Review

<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

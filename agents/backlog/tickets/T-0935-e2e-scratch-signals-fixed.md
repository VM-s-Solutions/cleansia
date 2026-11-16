---
id: T-0935
title: "The E2E phase 2 scratch runs' real defects fixed: a ticked card payment keeps its card, and the quote refuses a start under 2 hours"
size: S
owner: Michael Chaban
created: 2026-11-16
updated: 2026-11-16
depends_on: []
blocks: [T-0934]
stories: []
adrs: []
layers: [backend, qa, docs]
security_touching: false
---

## Context

Mike, 16 November 2026, on the signals the T-0934 build's scratch runs raised: "You can fix the issues (if those
are)", before the phase 2 run of 18 and 19 November.

## Doing

1. PAY-022: on DEV a card the customer ticked to keep was captured and then switched off, because the two Stripe
   events of one web payment were processed 0.4 s apart and the later one counted the card it was capturing among
   the cards it replaces (read with the new read-only `sql-scripts/dev-e2e-read-saved-cards.sql`). The capture now
   never replaces the card itself.
2. BOOK-014: `QuoteOrder` priced a start under the 2 h floor, while `/product/business-rules#booking-window` says it
   is refused, not priced. The quote now applies `CreateOrder`'s future and lead-time rules with its keys.
3. CANCEL-051: the case asked for a customer notice on a partial refund, which no docs page promises: a catalogue
   error, corrected with the spec. PAYOUT-045's invoice-detail clause stays (docs/flows/pay-and-payouts.md says the
   detail carries the constant symbol) and gains an API assertion on the detail.
4. The home page's quick quote sent a placeholder 09:00 start for a date with no time, which the quote now refuses
   when it is past or under 2 h: it sends a start only once a time is chosen, and drops a time the new day cannot take.

## Not doing

- The DEV outage of 15 November (an environment fault).
- The T-0933 review's out-of-scope rows R11 to R15 (Mike picks).
- A customer notice for a partial refund (a product choice for Mike).

## Done

One PR merged with CI green.

## Work record

- 2026-11-16 — Filed and fixed on `fix/e2e-scratch-signals`.
- 2026-11-16 — The review's rows fixed on the branch: PAYOUT-045's detail clause restored with an API assertion, the quick quote's placeholder start, case ids out of source comments, the e2e quote assertion made hard, and the API contract page.

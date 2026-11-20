---
id: T-0940
title: "The follow-ups found while fixing the phase-2 findings: one error text per key across the clients, receipt PDFs that are repaired, the iOS phone step, the web checkbox's label, and Apple Pay on iOS"
size: L
owner: Michael Chaban
created: 2026-11-19
updated: 2026-11-19
depends_on: [T-0939]
blocks: []
stories: []
adrs: [adr-0083]
layers: [backend, frontend, ios, qa, docs]
security_touching: false
---

## Context

Mike, 19 November 2026, on the phase-2 report: "Fix all of them by waves making the fixes that pays off in long term,
not quick fixes". This is wave 4: what waves 1 and 2 found outside the phase-2 findings, and the drift F-095's check
measured. His rulings (AskUserQuestion): every listed item is fixed, after the DEV proof of T-0939; each error key
reads one way per language on every client, the wording most clients already use, a tie going to the Android customer
app, and the check then runs in CI.

Every row was ground-truthed against master d4bc9f045 before filing.

## Doing

1. Error texts: `agents/tools/check-error-text-parity.mjs` found 237 of 412 backend error keys reading differently
   across the three webs, the two Android apps and iOS (968 key-language pairs). Each key and language takes the
   wording most clients already use, a tie going to the Android customer app, and the check then runs in CI.
2. A receipt's fiscal retry stamps the fiscal code before it renders and uploads the PDF
   (`ReceiptService.RetryFiscalRegistrationAsync`), so an upload fault leaves a receipt whose PDF lacks the code and
   which the retry job never picks again (T-0937 review, R9). The PDF stays repairable once the code is stamped.
3. In fiscal mode None a failed first receipt upload is never repaired: the redelivery acknowledges it, nothing
   renders the PDF again, the e-mail is never sent, and a download reaches a missing blob (T-0937 review, R10). A
   committed receipt without its file is rendered again.
4. The iOS first-sign-in phone step returns silently on Save when the profile has not loaded, and shows a refusal
   only as a snackbar: the iOS twin of F-089, which Android closed in T-0938. It loads the profile and shows a
   refusal under the phone field, as Android does.
5. The shared web checkbox puts its id on the PrimeNG host instead of the inner input, so its label does not name the
   box and a checkbox with a label has no accessible name (the customer login's *remember me*). The label names the
   box it ticks.
6. Apple Pay on iOS: the card sheet offers card only, because `StripePaymentController` sets no Apple Pay
   configuration and the app holds no merchant entitlement. Needs from Mike first: an Apple Merchant ID, its Stripe
   Apple Pay certificate, and the Apple Pay capability on the customer App ID. Then the sheet offers Apple Pay.
7. F-088, completed (F-109 of the fix-proof run): with every intent naming card only (T-0938), both PaymentSheets
   still offered *Pay with Link* and Link's save box on DEV on 20 November. Mike's ruling stands, Link off in code on
   every client: each app's PaymentSheet configuration turns Link off, the reason the card-only intents did not is
   established, and the sheets are shown without Link on DEV.

## Not doing

- PRO; store uploads; the partner apps' payment flows (they take no card).

## Done

Merged with CI green, each fix with a test where the platform allows one, the error-text check running in CI, and
DEV deployed with the wave; Apple Pay waits for Mike's merchant set-up if it has not come.

## Work record

- 2026-11-19 — Filed after Mike's rulings, on `fix/t0939-wave3-harness-and-dev-proof`.
- 2026-11-20 — F-088's completion added as item 7, after the fix-proof run showed Link still offered (F-109); started
  on `fix/t0940-wave4-follow-ups`.
- 2026-11-20 — Review of the wave; recorded for Mike's ruling before merge. Item 1: the majority rule did not settle
  150 of the 963 drifting pairs (ties the Android customer app is not part of); they were broken by a chain, iOS, then
  the Android partner app, the admin web, the partner web and the customer web (98 went to the admin web, 52 to iOS;
  every pair with its texts is in the wave's `texts/applied.json` under `unresolved`). One pair departs from the
  majority: `credit.cash_not_collectable_on_credit_order` in English reads *order*, not *booking*, because only a
  cleaner sees it. Four keys now read in the wrong context on a surface that shows them and wait for one wording
  each: `legal.document_not_found` (the customer and admin webs say *This job cannot be taken right now.*),
  `order.unpaid_receivable` (the customer web points to a *Payments* section it does not have, and the sentence names
  only a new booking although a recurring confirm also sends it), `order.not_found` and
  `order.in_progress_cannot_cancel` (customer vocabulary on the partner web and the Android partner app). The iOS
  birth-date wheels' range (yesterday back to 120 years, every iOS birth-date field) was added with item 4, so a
  refused birth date does not land under the phone field; it is outside the listed items, and Mike keeps it or takes
  it out.

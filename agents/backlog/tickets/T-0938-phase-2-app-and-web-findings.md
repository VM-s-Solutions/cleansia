---
id: T-0938
title: "The phase-2 app and web findings fixed: consent labels, the iOS order detail on push, the Android phone save, one cash notice, Stripe Link off, one Ukrainian wording"
size: L
owner: Michael Chaban
created: 2026-11-19
updated: 2026-11-19
depends_on: [T-0934]
blocks: [T-0939]
stories: []
adrs: []
layers: [android, ios, frontend, backend, docs]
security_touching: false
---

## Context

Mike, 19 November 2026, on the phase-2 E2E report (T-0934, `e2e/runs/2026-11-18/findings.md`): "Fix all of them by
waves making the fixes that pays off in long term, not quick fixes". The findings are fixed in three waves, T-0937,
T-0938 and T-0939, one PR each. This is wave 2. His rulings (AskUserQuestion): Stripe Link is turned off in code, on
every intent and client; the wrong-password refusal reads *Невірний пароль.* in Ukrainian on every client, and a
check keeps one error key's text the same across clients in each language (if the drift that exists today is more
than a handful of keys, the list goes to Mike before it is fixed); DEV is deployed after the wave.

Every row was ground-truthed against master c24378926 before filing.

## Doing

1. F-093: the Android consent checkbox falls back to the terms description
   (`core/.../CleansiaConsentCheckbox.kt:47`), so the card-saving and early-performance ticks are announced as the
   terms (`CardSavingConsent.kt:30-35`, `ConfirmStep.kt:491-496`). The checkbox takes no default: every caller names
   its own consent. The iOS and web labels of the same ticks are checked and held to the same rule.
2. F-092: the iOS customer's open order detail does not refresh on the order's pushes; `OrderEventBus.swift` is only a
   seam. Pushes now feed it and the open detail refetches, as on Android (`OrderDetailViewModel.kt:314`), and the
   docs say so.
3. F-089: on Android, *Save and continue* on the first-sign-in phone step once did nothing visible on a healthy DEV.
   It is reproduced on the emulator with a screen recording and fixed at its cause, and the refusal shows inline.
4. F-082: the customer web shows the cash-cleared notice twice, in the payment step's status line and as a toast. It
   keeps the status line, and the docs quote the catalogue's text.
5. F-088: Stripe Link appears because the intents use automatic payment methods (`StripeClient.cs:364`, `:503`). Every
   intent and payment sheet names its payment methods in code (card, plus Apple Pay and Google Pay where offered), so
   Link is off on every client whatever the Dashboard says; the case and the docs say so.
6. F-095: `validation.invalid_password` reads four ways in Ukrainian. Every client says *Невірний пароль.*, and a check
   fails when one error key reads differently between clients in the same language.

## Not doing

- Live refresh of the partner apps' open job detail: the docs say neither platform does it.
- F-089's *Ready to book* rule: by design (`docs/mobile-app/patterns.md`).
- Store uploads; PRO.

## Done

Merged with CI green on Android, iOS and the web, each fix with a test where the platform allows one, and DEV is
deployed with the wave.

## Work record

- 2026-11-19 — Filed with T-0937 and T-0939, after Mike's rulings.
- 2026-11-19 — Started on `fix/t0938-wave2-apps-and-web`, after T-0937 merged as `c76007860`.
- 2026-11-19 — Built on `fix/t0938-wave2-apps-and-web`, one review and its in-scope rows fixed. F-088: every Stripe
  intent, subscription and Checkout Session names card as its only payment method, so Link stays off whatever the
  Dashboard enables. F-093: Android's consent checkbox has no default label and every tick names its own consent
  (pinned by `ConsentCatalogTest`); the web's ticks are pinned by specs and iOS was checked. F-092: the iOS
  customer's pushes feed `OrderEventBus`, so an open detail refetches on its order's push and on return to the app.
  F-089: two silent paths on Android are closed: the network interceptor no longer swallows a read timeout, and the
  phone step shows a refused save under the phone field (reproduced on DEV with a recording); the birth-date wheels
  offer only the days the server accepts. F-082: the cash-cleared notice shows once, in the status line, on the
  booking and the recurring forms. F-095: every client says *Невірний пароль.*, and
  `agents/tools/check-error-text-parity.mjs` measured 237 of 412 error keys reading differently across the six
  clients, over Mike's line of five, so it is committed unwired and the list went to Mike.
- 2026-11-19 — Shipped in #389 (merge `d4bc9f045`), CI green on every check; DEV deploy dispatched after the merge
  (run 44601839102). Mike's ruling on the drift (majority wording per key and language, a tie to the Android customer
  app, then the check in CI) is T-0940. Reported, not fixed: an order intent opened before the deploy can still offer
  Link until it is replaced; an undeclared consistency violation at `OrdersListViewModel.kt:92`; 13 pre-existing
  Android lint errors that CI does not run; the iOS open detail polls every 30 s where Android polls every 5 minutes.

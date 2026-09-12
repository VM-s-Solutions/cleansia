---
id: T-0694
title: The order.confirmed push key has a fulfilment name and a money meaning
status: todo
size: M
owner: —
created: 2026-09-08
updated: 2026-09-08
depends_on: [T-0691]
blocks: []
stories: []
adrs: [ADR-0057, ADR-0045]
layers: [backend, frontend, android, ios]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

After ADR-0057, `Confirmed` is a fulfilment fact — a cleaner took the job. The push key
`order.confirmed` still fires for a **money** event: the customer's card settled. The name now points
at the opposite axis from its meaning.

The customer still needs telling that their payment landed, so the notification itself is correct and
must stay. Only the key is wrong.

T-0691 left it deliberately: renaming costs ten locale files across two mobile platforms plus the feed
catalogue, for no behaviour change. The two copy guards that forbid it claiming a cleaner
(`PushLocKeyCatalogTests`, `NotificationTemplatesTest`) are unaffected and remain true either way.

## Acceptance criteria

- [ ] **AC1** — The key names the money axis (e.g. `order.payment_received`), and every producer,
      catalogue entry and locale file moves with it.
- [ ] **AC2** — All five locales in every app that can receive it; the error-contract and push-key
      parity guards stay green.
- [ ] **AC3** — Deep links and tap-routing for the renamed key still resolve.
- [ ] **AC4** — A migration story for devices holding the old key, or an explicit statement that none is
      needed because pushes are not replayed.

## Out of scope

- The notification's copy or its trigger. Both are correct.

## Implementation notes

Cost centre is the locale files, not the code. Ten across two mobile platforms plus the feed catalogue.
Check whether anything persists the key (a notification feed row) before assuming a rename is stateless
— AC4 exists because of that.

## Status log

- 2026-09-08 — filed from the T-0691 out-of-scope list.

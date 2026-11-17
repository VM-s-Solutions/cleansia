---
id: T-0936
title: "A partial refund tells the customer, and the T-0933 review's rows R11 to R15 are fixed"
size: M
owner: Michael Chaban
created: 2026-11-17
updated: 2026-11-17
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, frontend, android, ios, qa, docs]
security_touching: false
---

## Context

Mike, 17 November 2026, on the T-0935 report: "You can fix R11-R15" and "A customer should get a notification" for a
partial refund. His rulings (AskUserQuestion): the notice is the full refund's own `order.refunded` (push and the
in-app feed), not a new event or text; it is built now; one DEV deploy follows the merge, before the phase 2 run of
18 and 19 November.

Every row was ground-truthed against master 9bb0b0b9e before work started.

## Doing

1. R11: the iOS partner dashboard shows +100 % when last month had no completed job, and truncates the percentage
   (`DashboardData.swift`, `monthDeltaPercent`). It now shows no trend at last month 0, as Android and the web do,
   and rounds as the web does (`dashboard.facade.ts`, `Math.abs(Math.round(percentChange))`).
2. R12: eight rules in four employee commands (`AdminUpdateEmployee`, `UpdateEmployee`, `UpdateIdentificationInfo`,
   `UpdateBankDetails`) answer `dispute.max_length_exceeded`. They now answer `common.max_length`, as F-068 did for
   `CancelInvoice`; every client that reaches them already translates it.
3. R13: the Android customer app's address manager renders the Mapbox map with a blank access token, the crash
   F-022 closed on the order detail. Without a token it now renders no map.
4. R14: the partner web's `api.order.not_confirmed` says an order must be confirmed "to start", but On my way answers
   it too. It now says the job is not confirmed yet, as the Android and iOS partner apps do, in all five locales.
5. R15: the Android partner dashboard's month trend truncates where the web rounds. It now rounds as the web does.
6. A partial line refund (`IssuePartialRefund`, `POST api/AdminRefund/partial`) now sends the customer
   `order.refunded`, keyed on the refund, as `AdminRefundOrder` does, and `RetryRefund` sends it when a retried
   partial refund goes through. E2E-CANCEL-051 and its suite test expect the notice again, and the docs list the
   partial refund among the notice's senders.

## Not doing

- `RetryRefund`'s handling of a dispute's refund.
- A notice that names the amount, or any new event or text.
- TestFlight or Play builds.
- The T-0935 residual: two different cards captured at the same moment for one user and currency can both stay
  active.

## Done

One PR with every CI gate green, merged to master and deployed to DEV (Mike's OK of 17 November) before the phase 2
run. Each code change has a test.
